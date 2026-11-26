using Autopilot.Core.Archives;

namespace Autopilot.Core.Executables;

/// <summary>
/// Reads what a Windows executable or DLL says about itself, as bytes on every host: nothing is
/// loaded or run, and only bounded ranges of the file are read.
/// </summary>
public static class Executable
{
    private const string AgilityCore = "D3D12Core.dll";

    /// <summary>
    /// The executable's bitness and the DLLs its import and delay-load import tables name, or
    /// <see cref="ProtectedExecutable"/> when the file is listed but Windows refuses its content.
    /// </summary>
    /// <exception cref="PeException">The file is not an x86 or x64 executable, or cannot be read.</exception>
    public static async Task<ExecutableReading> ReadAsync(string path, CancellationToken cancellationToken)
    {
        using PeImage? image = await OpenUnlessProtectedAsync(path, cancellationToken);
        if (image is null)
        {
            return new ProtectedExecutable();
        }

        IReadOnlySet<string> imports = await ImportTables.StaticAsync(image, cancellationToken);
        IReadOnlySet<string> delayImports = await ImportTables.DelayAsync(image, cancellationToken);
        return new ReadableExecutable(image.Bitness, imports, delayImports);
    }

    /// <summary>
    /// The file version stamped in the file's version resource, such as "310.8.0" (a fourth part
    /// of zero is dropped), or "" when it carries none.
    /// </summary>
    /// <exception cref="PeException">The file is not an x86 or x64 executable, or cannot be read.</exception>
    public static async Task<string> FileVersionAsync(string path, CancellationToken cancellationToken)
    {
        using PeImage image = await OpenAsync(path, cancellationToken);
        return await VersionResource.ReadAsync(image, cancellationToken);
    }

    /// <summary>The Agility SDK the file asks for, or null when it does not export <c>D3D12SDKVersion</c>.</summary>
    /// <exception cref="PeException">The file is not an x86 or x64 executable, or cannot be read.</exception>
    public static async Task<AgilityRequest?> AgilityRequestAsync(string path, CancellationToken cancellationToken)
    {
        using PeImage image = await OpenAsync(path, cancellationToken);
        return await AgilityExports.ReadAsync(image, cancellationToken);
    }

    /// <summary>
    /// Whether this Windows can meet the executable's Agility SDK request: null when it can, or
    /// when anything leaves a doubt (no request, no <c>D3D12Core.dll</c> in
    /// <paramref name="systemFolder"/>, a system DLL that cannot be read, a path that leaves the
    /// executable's folder).
    /// </summary>
    /// <param name="executablePath">The game's executable.</param>
    /// <param name="systemFolder">Windows' <c>System32</c> folder, where its own <c>D3D12Core.dll</c> lives.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <exception cref="PeException">The executable itself is not an x86 or x64 executable, or cannot be read.</exception>
    public static async Task<AgilityUnmet?> CheckAgilityAsync(string executablePath, string systemFolder, CancellationToken cancellationToken)
    {
        AgilityRequest? request = await AgilityRequestAsync(executablePath, cancellationToken);
        if (request is null || ShipsOwnCore(executablePath, request.Path))
        {
            return null;
        }

        uint? system = await SystemVersionAsync(systemFolder, cancellationToken);
        return system < request.Version ? new AgilityUnmet(executablePath, request.Version, request.Path, system.Value) : null;
    }

    // Null when the file is listed but Windows refuses its content: a protected executable.
    private static async Task<PeImage?> OpenUnlessProtectedAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await PeImage.OpenAsync(path, cancellationToken);
        }
        catch (UnauthorizedAccessException) when (File.Exists(path))
        {
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            throw new PeException(PeFailure.Unreadable, path, e);
        }
    }

    private static async Task<PeImage> OpenAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await PeImage.OpenAsync(path, cancellationToken);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new PeException(PeFailure.Unreadable, path, e);
        }
    }

    // A path that would leave the executable's folder is never followed, and leaves a doubt.
    private static bool ShipsOwnCore(string executablePath, string exportedPath)
    {
        string relative = exportedPath.Trim('/', '\\');
        string folder = Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? "";
        try
        {
            return File.Exists(SafePath.Join(folder, (relative.Length == 0 ? "." : relative) + "/" + AgilityCore));
        }
        catch (UnsafePathException)
        {
            return true;
        }
    }

    private static async Task<uint?> SystemVersionAsync(string systemFolder, CancellationToken cancellationToken)
    {
        string core = Path.Join(systemFolder, AgilityCore);
        if (!File.Exists(core))
        {
            return null;
        }

        try
        {
            return (await AgilityRequestAsync(core, cancellationToken))?.Version;
        }
        catch (PeException)
        {
            return null;
        }
    }
}
