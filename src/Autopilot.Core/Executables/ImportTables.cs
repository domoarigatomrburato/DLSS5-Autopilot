using System.Collections.Frozen;
using System.Text;

namespace Autopilot.Core.Executables;

/// <summary>The DLL names in an executable's import and delay-load import tables.</summary>
internal static class ImportTables
{
    /// <summary>Descriptors read at most, so a table with no terminator ends.</summary>
    private const int MaxDescriptors = 1024;

    /// <summary>A DLL name is a short string; the bytes read for one, terminator included.</summary>
    private const int MaxNameBytes = 256;

    private static readonly Layout _static = new(DataDirectory.Import, DescriptorSize: 20, NameOffset: 12, NeedsRvaFlag: false);

    // Attributes bit 0 set means the descriptor holds RVAs; old linkers wrote virtual addresses,
    // which cannot be resolved without the image base, so those descriptors are skipped.
    private static readonly Layout _delay = new(DataDirectory.DelayImport, DescriptorSize: 32, NameOffset: 4, NeedsRvaFlag: true);

    public static Task<IReadOnlySet<string>> StaticAsync(PeImage image, CancellationToken cancellationToken) =>
        ReadAsync(image, _static, cancellationToken);

    public static Task<IReadOnlySet<string>> DelayAsync(PeImage image, CancellationToken cancellationToken) =>
        ReadAsync(image, _delay, cancellationToken);

    private static async Task<IReadOnlySet<string>> ReadAsync(PeImage image, Layout layout, CancellationToken cancellationToken)
    {
        List<string> names = [];
        if (image.DirectoryRva(layout.Directory) is { } rva)
        {
            // One read for the whole table; it stops short at the end of its section's raw data.
            byte[] table = await image.ReadRvaAsync(rva, layout.DescriptorSize * MaxDescriptors, cancellationToken);
            for (int at = 0; at + layout.DescriptorSize <= table.Length; at += layout.DescriptorSize)
            {
                ReadOnlyMemory<byte> descriptor = table.AsMemory(at, layout.DescriptorSize);
                if (!descriptor.Span.ContainsAnyExcept((byte)0))
                {
                    break;
                }

                if (layout.Resolvable(descriptor.Span) && await image.ReadAsciiAsync(PeImage.U32(descriptor.Span, layout.NameOffset), MaxNameBytes, cancellationToken) is { Length: > 0 } name)
                {
                    names.Add(LowerCase(name));
                }
            }
        }

        return names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    // Names are printable ASCII by now, so ASCII case rules are the whole story.
    private static string LowerCase(string name) =>
        string.Create(name.Length, name, static (lower, source) => Ascii.ToLower(source, lower, out _));

    private sealed record Layout(DataDirectory Directory, int DescriptorSize, int NameOffset, bool NeedsRvaFlag)
    {
        public bool Resolvable(ReadOnlySpan<byte> descriptor) => !NeedsRvaFlag || (PeImage.U32(descriptor, 0) & 1) != 0;
    }
}
