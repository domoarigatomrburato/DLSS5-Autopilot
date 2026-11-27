using System.Xml;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Library.Stores;

/// <summary>Reads the executable a GDK game declares in its <c>MicrosoftGame.config</c>, as untrusted XML.</summary>
internal static class MicrosoftGameConfig
{
    private static readonly XmlReaderSettings _settings = UntrustedXml.Settings(StoreFiles.MaxBytes, async: true);

    /// <summary>
    /// The <c>ExecutableList</c> entry for <c>PC</c>, else the first, as a full path below the game
    /// folder when it exists; null when there is no config, it is malformed, or the file is missing.
    /// </summary>
    public static async Task<string?> PcExecutableAsync(string folder, CancellationToken cancellationToken)
    {
        byte[]? bytes = await StoreFiles.ReadAsync(Path.Join(folder, "MicrosoftGame.config"), cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return StoreFiles.ExistingExecutable(folder, await ChosenAsync(bytes));
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static async Task<string?> ChosenAsync(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        using var reader = XmlReader.Create(stream, _settings);
        string? first = null;
        while (await reader.ReadAsync())
        {
            if (reader.NodeType != XmlNodeType.Element || !reader.LocalName.Equals("Executable", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? name = reader.GetAttribute("Name");
            if (string.Equals(reader.GetAttribute("TargetDeviceFamily"), "PC", StringComparison.OrdinalIgnoreCase) && name is not null)
            {
                return name;
            }

            first ??= name;
        }

        return first;
    }
}
