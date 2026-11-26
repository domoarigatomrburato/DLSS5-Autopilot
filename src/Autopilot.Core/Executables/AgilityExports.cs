namespace Autopilot.Core.Executables;

/// <summary>
/// The <c>D3D12SDKVersion</c> and <c>D3D12SDKPath</c> exports the D3D12 loader reads from a
/// process's executable, found by walking the export name table by range.
/// </summary>
internal static class AgilityExports
{
    /// <summary>Export names walked at most; an executable exports far fewer.</summary>
    public const int MaxNames = 65_536;

    /// <summary>A path's bytes read at most, terminator included, as Windows' MAX_PATH.</summary>
    public const int MaxPathBytes = 260;

    private const int NamesPerRead = 1024;
    private const int ExportDirectorySize = 40;

    private static readonly byte[] _versionName = "D3D12SDKVersion\0"u8.ToArray();
    private static readonly byte[] _pathName = "D3D12SDKPath\0"u8.ToArray();

    public static async Task<AgilityRequest?> ReadAsync(PeImage image, CancellationToken cancellationToken)
    {
        byte[] directory = image.DirectoryRva(DataDirectory.Export) is { } rva
            ? await image.ReadRvaAsync(rva, ExportDirectorySize, cancellationToken)
            : [];
        if (directory.Length < ExportDirectorySize)
        {
            return null;
        }

        Found found = await FindAsync(image, new ExportDirectory(directory), cancellationToken);
        byte[] version = found.Version is { } at ? await image.ReadRvaAsync(at, 4, cancellationToken) : [];
        if (version.Length < 4)
        {
            return null;
        }

        string path = found.Path is { } p ? await PathAsync(image, p, cancellationToken) : "";
        return new AgilityRequest(PeImage.U32(version, 0), path);
    }

    private static async Task<Found> FindAsync(PeImage image, ExportDirectory directory, CancellationToken cancellationToken)
    {
        Found found = new(null, null);
        int count = (int)Math.Min(directory.NumberOfNames, MaxNames);
        for (int start = 0; start < count && !found.Complete; start += NamesPerRead)
        {
            int batch = Math.Min(NamesPerRead, count - start);
            byte[] names = await ReadTableAsync(image, directory.Names, start, 4, batch, cancellationToken);
            byte[] ordinals = await ReadTableAsync(image, directory.Ordinals, start, 2, batch, cancellationToken);
            int read = Math.Min(names.Length / 4, ordinals.Length / 2);
            for (int i = 0; i < read && !found.Complete; i++)
            {
                found = await MatchAsync(image, directory, found, PeImage.U32(names, i * 4), PeImage.U16(ordinals, i * 2), cancellationToken);
            }

            if (read < batch)
            {
                break;
            }
        }

        return found;
    }

    private static Task<byte[]> ReadTableAsync(PeImage image, uint table, int start, int width, int count, CancellationToken cancellationToken) =>
        image.ReadRvaAsync(table + ((long)start * width), count * width, cancellationToken);

    private static async Task<Found> MatchAsync(PeImage image, ExportDirectory directory, Found found, uint nameRva, ushort ordinal, CancellationToken cancellationToken)
    {
        byte[] name = await image.ReadRvaAsync(nameRva, _versionName.Length, cancellationToken);
        bool isVersion = name.AsSpan().SequenceEqual(_versionName);
        bool isPath = name.AsSpan().StartsWith(_pathName);
        if (!isVersion && !isPath)
        {
            return found;
        }

        uint? target = await FunctionAsync(image, directory, ordinal, cancellationToken);
        return isVersion ? found with { Version = found.Version ?? target } : found with { Path = found.Path ?? target };
    }

    private static async Task<uint?> FunctionAsync(PeImage image, ExportDirectory directory, ushort ordinal, CancellationToken cancellationToken)
    {
        if (ordinal >= directory.NumberOfFunctions)
        {
            return null;
        }

        byte[] rva = await image.ReadRvaAsync(directory.Functions + (ordinal * 4L), 4, cancellationToken);
        return rva.Length == 4 ? PeImage.U32(rva, 0) : null;
    }

    // D3D12SDKPath is a `const char*` variable: follow the pointer. A string stored inline at
    // the export (a char array) is taken when the pointer leads nowhere; otherwise it is unreadable.
    private static async Task<string> PathAsync(PeImage image, uint rva, CancellationToken cancellationToken)
    {
        int width = image.Bitness == Bitness.Bits64 ? 8 : 4;
        byte[] pointer = await image.ReadRvaAsync(rva, width, cancellationToken);
        if (pointer.Length == width)
        {
            ulong address = width == 8 ? PeImage.U64(pointer, 0) : PeImage.U32(pointer, 0);
            if (address >= image.ImageBase && address - image.ImageBase <= uint.MaxValue
                && await image.ReadAsciiAsync((long)(address - image.ImageBase), MaxPathBytes, cancellationToken) is { } pointed)
            {
                return pointed;
            }
        }

        return await image.ReadAsciiAsync(rva, MaxPathBytes, cancellationToken) ?? "";
    }

    private sealed record Found(uint? Version, uint? Path)
    {
        public bool Complete => Version is not null && Path is not null;
    }

    private sealed class ExportDirectory(byte[] bytes)
    {
        public uint NumberOfFunctions { get; } = PeImage.U32(bytes, 20);

        public uint NumberOfNames { get; } = PeImage.U32(bytes, 24);

        public uint Functions { get; } = PeImage.U32(bytes, 28);

        public uint Names { get; } = PeImage.U32(bytes, 32);

        public uint Ordinals { get; } = PeImage.U32(bytes, 36);
    }
}
