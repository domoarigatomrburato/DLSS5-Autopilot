using System.Text.Json;
using System.Text.Json.Nodes;
using Autopilot.Core.Storage;

namespace Autopilot.Core.State;

/// <summary>A store file written whole, atomically, as indented JSON a person can read and edit.</summary>
internal static class StoreWrite
{
    /// <summary>
    /// Writes <paramref name="document"/> to <paramref name="path"/> through <see cref="AtomicFile"/>,
    /// blocking until it is done: the stores are synchronous, so their writes belong off the UI thread.
    /// The write runs off the caller's context, so a blocked thread is never the one its continuations
    /// need, and the caller's thread keeps any mutex it holds.
    /// </summary>
    /// <exception cref="IOException">The write or the move failed (a full drive included).</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or the file cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static void Whole(string path, JsonObject document, Func<Stream, Stream> through, TimeProvider time, CancellationToken cancellationToken) =>
        Task.Run(() => AtomicFile.WriteAsync(path, (file, token) => WriteAsync(through(file), document, token), check: null, time, cancellationToken), cancellationToken)
            .GetAwaiter().GetResult();

    private static async Task WriteAsync(Stream file, JsonObject document, CancellationToken cancellationToken)
    {
        await using Utf8JsonWriter writer = new(file, new JsonWriterOptions { Indented = true });
        document.WriteTo(writer);
        await writer.FlushAsync(cancellationToken);
    }
}
