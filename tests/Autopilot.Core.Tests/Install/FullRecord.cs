using System.Text.Json.Nodes;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A record holding something in every part, every entry state and both backup kinds, and a check that two records say the same.</summary>
internal static class FullRecord
{
    private static readonly FileFacts _ours = new("6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b", 214_016);

    private static readonly FileFacts _games = new("d4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35", 180_224);

    /// <summary>The record, installed in <c>Binaries/Win64</c>.</summary>
    public static InstallRecord Value => WithEffects(Recorded.Install("Binaries/Win64", Entries) with
    {
        ToolVersion = "1.2.3",
        State = RecordState.Installing,
        Game = new RecordedGame(["Game.exe", "GameExpansion.exe"], Bitness.Bits64, GraphicsApi.DX12, GameSource.BattleNet),
        Route = RouteId.OptiScaler,
        Choices = new JsonObject { ["provider"] = "dlss", ["dials"] = new JsonObject { ["sharpness"] = 0.25, ["modes"] = new JsonArray("a", "b") } },
        Components = new Dictionary<string, string> { ["reshade"] = "6.4.1", ["optiscaler"] = "0.7.9" },
    });

    // What the install did beyond its entries, and what it said.
    private static InstallRecord WithEffects(InstallRecord record) => record with
    {
        ConfigEdits = [new("rtx.conf", null, GameFolders.RtxNeuralKey, $"{GameFolders.RtxNeuralKey} = False", false, false, "True", false)],
        Registrations = [new(RegisteredLayer.VulkanLayer64, false, @"C:\Users\p\AppData\Local\invidia-claritatis\reshade-vulkan\ReShade64.json"), new(RegisteredLayer.OpenXr, true, @"C:\Program Files\ReShade\ReShade64_XR.json")],
        GraphicsPreferences = [new(@"C:\Games\Game\Binaries\Win64\Game.exe", "SwapEffectUpgradeEnable=1;", "SwapEffectUpgradeEnable=1;GpuPreference=2;"), new(@"C:\Games\Game\host64\dlss5-feed-host64.exe", null, "GpuPreference=2;")],
        CreatedFolders = ["Binaries/Win64/reshade-shaders", "Binaries/Win64/reshade-shaders/Shaders"],
        Notes = ["ReShade loads into this game only."],
        Warnings = ["The layer loads into every Vulkan application."],
        Skipped = ["VR: the OpenXR layer is 64-bit only - skipped"],
    };

    private static RecordEntry[] Entries =>
    [
        new("Binaries/Win64/dxgi.dll", EntryClass.Component, EntryState.Written, _ours, Original(FileAttributes.ReadOnly | FileAttributes.Hidden, BackupKind.Backup)),
        new("Binaries/Win64/OptiScaler.ini", EntryClass.Config, EntryState.Pending, _ours, null),
        new("Binaries/Win64/d3dcompiler_47.dll", EntryClass.Component, EntryState.MovedAside, null, Original(FileAttributes.Normal, BackupKind.Dlss5Off)),
        new("Plugins/nvngx_dlss.dll", EntryClass.Component, EntryState.RestoreFailed, _ours, Original(FileAttributes.Archive, BackupKind.Backup)),
        new("Binaries/Win64/nvngx.dll", EntryClass.Component, EntryState.Stuck, _ours, null),
    ];

    /// <summary>Checks <paramref name="actual"/> says what <paramref name="expected"/> does, part by part.</summary>
    public static void AssertSame(InstallRecord expected, InstallRecord actual)
    {
        Assert.AreEqual((expected.FormatVersion, expected.ToolVersion, expected.State, expected.InstallFolder, expected.Route), (actual.FormatVersion, actual.ToolVersion, actual.State, actual.InstallFolder, actual.Route));
        Assert.AreEqual(expected.Game with { Executables = [] }, actual.Game with { Executables = [] });
        Assert.IsTrue(JsonNode.DeepEquals(expected.Choices, actual.Choices), actual.Choices.ToJsonString());
        CollectionAssert.AreEquivalent(expected.Components.ToList(), actual.Components.ToList());
        SameLists(expected, actual);
    }

    private static void SameLists(InstallRecord expected, InstallRecord actual)
    {
        CollectionAssert.AreEqual(expected.Game.Executables.ToList(), actual.Game.Executables.ToList());
        CollectionAssert.AreEqual(expected.Entries.ToList(), actual.Entries.ToList());
        CollectionAssert.AreEqual(expected.ConfigEdits.ToList(), actual.ConfigEdits.ToList());
        CollectionAssert.AreEqual(expected.Registrations.ToList(), actual.Registrations.ToList());
        CollectionAssert.AreEqual(expected.GraphicsPreferences.ToList(), actual.GraphicsPreferences.ToList());
        CollectionAssert.AreEqual(expected.CreatedFolders.ToList(), actual.CreatedFolders.ToList());
        CollectionAssert.AreEqual(expected.Notes.ToList(), actual.Notes.ToList());
        CollectionAssert.AreEqual(expected.Warnings.ToList(), actual.Warnings.ToList());
        CollectionAssert.AreEqual(expected.Skipped.ToList(), actual.Skipped.ToList());
    }

    private static OriginalFile Original(FileAttributes attributes, BackupKind kind) =>
        new(_games, attributes, new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc), kind);
}
