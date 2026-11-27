using System.Text.Json;
using System.Text.Json.Serialization;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Library;

/// <summary>
/// <c>library.json</c> as written. Members a reader does not know are skipped, so a later section
/// (the compatibility rows) can be added beside <see cref="Games"/> with its own version, without
/// changing <see cref="Schema"/>.
/// </summary>
internal sealed record CacheFile(int Schema, string ToolVersion, int? GpuSm, IReadOnlyList<CachedEntry> Games)
{
    /// <summary>The file is untrusted on read; a library of thousands of games is a few MiB.</summary>
    private const long MaxBytes = 64L * 1024 * 1024;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters =
        {
            new NamedOnly<GameSource>(),
            new NamedOnly<Bitness>(),
            new NamedOnly<GraphicsApi>(),
            new NamedOnly<ApiReason>(),
            new NamedOnly<GameReadFailure>(),
            new NamedOnly<InstallState>(),
        },
    };

    /// <summary>Whether every game is there and every path in it is absolute.</summary>
    public bool IsWellFormed() => Games.All(game => game is not null && game.IsWellFormed());

    /// <summary>The file at <paramref name="path"/>, or null when it cannot be opened, is too large or does not parse.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<CacheFile?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        byte[]? bytes = await BoundedFile.ReadAsync(path, MaxBytes, cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CacheFile>(UntrustedJson.Utf8(bytes).Span, _json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes the file atomically, through <paramref name="through"/>, which wraps the temporary file's stream.</summary>
    /// <exception cref="IOException">The write or the move failed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or the file cannot be written.</exception>
    public Task WriteAsync(string path, Func<Stream, Stream> through, TimeProvider clock, CancellationToken cancellationToken) =>
        AtomicFile.WriteAsync(path, (file, token) => JsonSerializer.SerializeAsync(through(file), this, _json, token), check: null, clock, cancellationToken);
}

/// <summary>One game as saved: its entry without the emulator profile, which is found again from the executable, and its stamp.</summary>
internal sealed record CachedEntry(
    string Name,
    string Folder,
    GameSource Source,
    string? Executable,
    string? StoreExecutable,
    IReadOnlyList<string> Candidates,
    Bitness? Bitness,
    GraphicsApi Api,
    string ApiExplanation,
    CachedDecision? Detected,
    bool ApiSetByHand,
    GameReadError? Error,
    bool Protected,
    string? InstallFolder,
    InstallState? Install,
    GameStamp Stamp)
{
    public static CachedEntry Of(GameEntry game) => new(
        game.Name,
        game.Folder,
        game.Source,
        game.Executable,
        game.StoreExecutable,
        game.Candidates,
        game.Bitness,
        game.Api,
        game.ApiExplanation,
        game.Detected is { } detected ? new(detected.Api, detected.Reason, detected.Explanation) : null,
        game.ApiSetByHand,
        game.Error,
        game.Protected,
        game.InstallFolder,
        game.Install,
        GameStamp.Of(game));

    /// <summary>Every path is absolute; the file is read as untrusted, and a relative path would be read against the working folder.</summary>
    public bool IsWellFormed() =>
        Path.IsPathFullyQualified(Folder)
        && new[] { Executable, StoreExecutable, InstallFolder }.All(path => path is null || Path.IsPathFullyQualified(path))
        && Candidates.All(path => path is not null && Path.IsPathFullyQualified(path));

    public GameEntry ToEntry() => new()
    {
        Name = Name,
        Folder = Folder,
        Source = Source,
        Executable = Executable,
        StoreExecutable = StoreExecutable,
        Candidates = Candidates,
        Bitness = Bitness,
        Api = Api,
        ApiExplanation = ApiExplanation,
        Detected = Detected is { } detected ? new ApiDecision(detected.Api, detected.Reason, detected.Explanation) : null,
        ApiSetByHand = ApiSetByHand,
        Error = Error,
        Protected = Protected,
        InstallFolder = InstallFolder,
        Install = Install,
    };
}

internal sealed record CachedDecision(GraphicsApi Api, ApiReason Reason, string Explanation);

/// <summary>Enums by name only: a number from a hand-edited or damaged file could be a value no code expects.</summary>
internal sealed class NamedOnly<TEnum>() : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
    where TEnum : struct, Enum;

