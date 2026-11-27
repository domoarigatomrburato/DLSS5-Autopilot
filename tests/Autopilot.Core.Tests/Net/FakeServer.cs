using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Autopilot.Core.Tests.Net;

/// <summary>
/// An in-memory server: answers each URL from a queue of scripted answers (the last one repeats),
/// records every request it receives, and tracks every body it hands out so a test can check that
/// each one was disposed.
/// </summary>
internal sealed class FakeServer(TimeProvider time) : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>> _routes = [];
    private readonly List<TrackedContent> _bodies = [];

    public List<ReceivedRequest> Requests { get; } = [];

    public bool EveryBodyDisposed => _bodies.TrueForAll(body => body.Disposed);

    public int BodiesHandedOut => _bodies.Count;

    public int RequestsTo(string url) => Requests.Count(request => request.Url == new Uri(url));

    /// <summary>The methods of the requests to <paramref name="url"/>, in order.</summary>
    public HttpMethod[] MethodsTo(string url) => [.. Requests.Where(request => request.Url == new Uri(url)).Select(request => request.Method)];

    public FakeServer On(string url, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        if (!_routes.TryGetValue(url, out Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>? queue))
        {
            queue = new();
            _routes[url] = queue;
        }

        queue.Enqueue(answer);
        return this;
    }

    public FakeServer On(string url, Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        On(url, (request, _) => Task.FromResult(answer(request)));

    public FakeServer Answer(string url, HttpStatusCode status, string body = "", params (string Name, string Value)[] headers) =>
        On(url, _ => Respond(status, Encoding.UTF8.GetBytes(body), declaredLength: null, headers));

    public FakeServer AnswerBytes(string url, byte[] body, long? declaredLength = null, HttpStatusCode status = HttpStatusCode.OK, params (string Name, string Value)[] headers) =>
        On(url, _ => Respond(status, body, declaredLength, headers));

    /// <summary>A body sent without a length, as a server that streams or lies by omission does.</summary>
    public FakeServer AnswerUndeclared(string url, byte[] body) =>
        On(url, _ =>
        {
            HttpResponseMessage response = Respond(HttpStatusCode.OK, body, declaredLength: null);
            response.Content.Headers.ContentLength = null;
            return response;
        });

    public FakeServer Redirect(string url, string location, HttpStatusCode status = HttpStatusCode.Found) =>
        On(url, _ => Respond(status, [], declaredLength: null, ("Location", location)));

    public FakeServer Throw(string url, Exception failure) =>
        On(url, (_, _) => Task.FromException<HttpResponseMessage>(failure));

    /// <summary>Headers arrive, then the body sends nothing until the read is cancelled.</summary>
    public FakeServer StallBody(string url) =>
        On(url, _ => Track(new HttpResponseMessage(HttpStatusCode.OK), new TrackedContent(new StalledStream(broken: false), declaredLength: 100)));

    /// <summary>A slow but live body: one byte after each gap on the server's clock.</summary>
    public FakeServer Trickle(string url, int bytes, TimeSpan gap) =>
        On(url, _ => Track(new HttpResponseMessage(HttpStatusCode.OK), new TrackedContent(new TrickleStream(time, bytes, gap), declaredLength: bytes)));

    /// <summary>Headers arrive, then the connection is reset on the first read of the body.</summary>
    public FakeServer BreakBody(string url) =>
        On(url, _ => Track(new HttpResponseMessage(HttpStatusCode.OK), new TrackedContent(new StalledStream(broken: true), declaredLength: 100)));

    /// <summary>The server never sends its headers until the request is cancelled.</summary>
    public FakeServer StallHeaders(string url) =>
        On(url, (_, cancellation) => StalledStream.Never<HttpResponseMessage>(cancellation));

    public HttpResponseMessage Respond(HttpStatusCode status, byte[] body, long? declaredLength, params (string Name, string Value)[] headers)
    {
        TrackedContent content = new(new MemoryStream(body), declaredLength ?? body.Length);
        HttpResponseMessage response = Track(new HttpResponseMessage(status), content);
        foreach ((string name, string value) in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri url = request.RequestUri ?? throw new InvalidOperationException("a request without a URL");
        Requests.Add(new(request.Method, url, request.Headers.UserAgent.ToString(), request.Headers.Range, time.GetUtcNow()));
        if (!_routes.TryGetValue(url.AbsoluteUri, out Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>? queue))
        {
            throw new InvalidOperationException($"the fake server has no answer for {url}");
        }

        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        return answer(request, cancellationToken);
    }

    private HttpResponseMessage Track(HttpResponseMessage response, TrackedContent content)
    {
        _bodies.Add(content);
        response.Content = content;
        return response;
    }

    private sealed class TrackedContent : HttpContent
    {
        private readonly Stream _body;

        public TrackedContent(Stream body, long declaredLength)
        {
            _body = body;
            Headers.ContentLength = declaredLength;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        public bool Disposed { get; private set; }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(_body);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => _body.CopyToAsync(stream);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            if (disposing)
            {
                _body.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

internal sealed record ReceivedRequest(HttpMethod Method, Uri Url, string UserAgent, RangeHeaderValue? Range, DateTimeOffset At);

/// <summary>Scripted failures of the fake server, kept beside <see cref="FakeServer.Throw"/>.</summary>
internal static class FakeServerFailures
{
    /// <summary>The host cannot be reached: the connection fails before any answer.</summary>
    public static FakeServer Unreachable(this FakeServer server, string url) =>
        server.Throw(url, new HttpRequestException(HttpRequestError.ConnectionError, "No route to host"));
}
