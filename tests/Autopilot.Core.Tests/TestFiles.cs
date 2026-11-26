using System.Text;

namespace Autopilot.Core.Tests;

/// <summary>Files written the way an earlier run or another process would, creating their folders.</summary>
internal static class TestFiles
{
    public static void Plant(string path, byte[] content)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    public static void Plant(string path, string text) => Plant(path, Encoding.UTF8.GetBytes(text));
}
