using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>What reading a game says about its executable: architecture, graphics API, protection, errors.</summary>
[TestClass]
public sealed class GameFactsTests(TestContext testContext) : IDisposable
{
    private const string Protected = "Choose this game's 'architecture' and 'graphics api' in the game's settings - Windows protects its executable, so they cannot be read from it.";

    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_readable_executable_gives_its_architecture_and_api_and_is_supported()
    {
        _ = _bed.Exe("Game/Game.exe", imports: "d3d11.dll");

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(Bitness.Bits64, entry.Bitness);
        Assert.AreEqual(GraphicsApi.DX11, entry.Api);
        Assert.AreEqual("imports d3d11.dll statically", entry.ApiExplanation);
        Assert.IsFalse(entry.ApiSetByHand);
        Assert.IsNull(entry.Unsupported);
    }

    [TestMethod]
    public async Task RUSE_draws_with_the_DirectX_9_chosen_by_hand_and_still_shows_the_DirectX_11_its_imports_say()
    {
        _ = _bed.Exe("Game/Game.exe", imports: "d3d11.dll");
        Choose(new GameChoices(GraphicsApi.DX9, null, null));

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(GraphicsApi.DX9, entry.Api);
        Assert.IsTrue(entry.ApiSetByHand);
        Assert.AreEqual("set by hand (detected DX11)", entry.ApiExplanation);
        Assert.AreEqual(GraphicsApi.DX11, entry.Detected?.Api);
    }

    [TestMethod]
    [DataRow(GraphicsApi.Unknown)]
    [DataRow(GraphicsApi.Dxgi)]
    public async Task An_api_no_one_can_choose_counts_as_no_choice(GraphicsApi chosen)
    {
        _ = _bed.Exe("Game/Game.exe", imports: "d3d11.dll");
        Choose(new GameChoices(chosen, null, null));

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(GraphicsApi.DX11, entry.Api);
        Assert.IsFalse(entry.ApiSetByHand);
    }

    [TestMethod]
    public async Task A_readable_executable_ignores_an_architecture_chosen_by_hand()
    {
        _ = _bed.Exe("Game/Game.exe", imports: "d3d11.dll");
        Choose(new GameChoices(null, Bitness.Bits32, null));

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(Bitness.Bits64, entry.Bitness);
        Assert.IsFalse(entry.Protected);
    }

    [TestMethod]
    public async Task DirectX_8_chosen_for_a_64_bit_executable_is_not_supported()
    {
        _ = _bed.Exe("Game/Game.exe", imports: "d3d9.dll");
        Choose(new GameChoices(GraphicsApi.DX8, null, null));

        GameEntry entry = await ReadAsync();

        Assert.AreEqual("DirectX 8 is 32-bit only, and this executable is 64-bit - set 'graphics api' to the one it really draws with in the game's settings.", entry.Unsupported);
    }

    [TestMethod]
    public async Task A_file_that_is_not_an_executable_is_an_error_naming_the_failure()
    {
        _ = _bed.NotAnExe("Game/Game.exe");

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(GameReadFailure.Unreadable, entry.Error?.Failure);
        Assert.AreEqual("Game.exe is not a Windows executable (no MZ signature).", entry.Error?.Explanation);
        Assert.AreEqual(entry.Error?.Explanation, entry.Unsupported);
    }

    [TestMethod]
    public async Task A_folder_without_an_executable_is_kept_with_the_reason_and_a_note()
    {
        _ = _bed.Folder("Game/saves");
        _bed.Records.Add(_bed.PathOf("Game"), InstallState.Installed);

        GameRead read = await _bed.ReadWithNotesAsync(_bed.Game("Some Game", "Game"), Ct);

        Assert.IsNull(read.Entry.Executable);
        Assert.AreEqual(new GameReadError(GameReadFailure.NoExecutable, "no executable found"), read.Entry.Error);
        Assert.AreEqual("No game executable found.", read.Entry.Unsupported);
        Assert.AreEqual(InstallState.Installed, read.Entry.Install);
        Assert.AreEqual($"no executable under {_bed.PathOf("Game")} - Some Game cannot be set up", Scans.Note<NoExecutableFound>(read.Notes));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_protected_executable_asks_for_both_choices()
    {
        _ = _bed.Exe("Game/Game.exe", imports: "d3d11.dll");
        _bed.Refuse("Game/Game.exe");

        GameEntry entry = await ReadAsync();

        Assert.IsTrue(entry.Protected);
        Assert.IsNull(entry.Bitness);
        Assert.IsNull(entry.Error);
        Assert.AreEqual(Protected, entry.Unsupported);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_protected_executable_with_only_its_architecture_chosen_still_asks_for_the_api()
    {
        _ = _bed.Exe("Game/Game.exe");
        _bed.Refuse("Game/Game.exe");
        Choose(new GameChoices(null, Bitness.Bits64, null));

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(Bitness.Bits64, entry.Bitness);
        Assert.AreEqual(Protected, entry.Unsupported);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_protected_executable_with_both_choices_is_supported()
    {
        _ = _bed.Exe("Game/Game.exe");
        _bed.Refuse("Game/Game.exe");
        Choose(new GameChoices(GraphicsApi.DX12, Bitness.Bits64, null));

        GameEntry entry = await ReadAsync();

        Assert.AreEqual((Bitness.Bits64, GraphicsApi.DX12), (entry.Bitness, entry.Api));
        Assert.IsNull(entry.Unsupported);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_protected_executable_beside_an_Agility_SDK_reads_DirectX_12_and_needs_only_its_architecture()
    {
        _ = _bed.Exe("Game/Game.exe");
        _ = _bed.Exe("Game/D3D12/D3D12Core.dll");
        _bed.Refuse("Game/Game.exe");
        Choose(new GameChoices(null, Bitness.Bits64, null));

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(GraphicsApi.DX12, entry.Api);
        Assert.Contains("Agility", entry.ApiExplanation);
        Assert.IsNull(entry.Unsupported);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_protected_executable_beside_an_Agility_SDK_still_asks_for_its_architecture()
    {
        _ = _bed.Exe("Game/Game.exe");
        _ = _bed.Exe("Game/D3D12/D3D12Core.dll");
        _bed.Refuse("Game/Game.exe");

        GameEntry entry = await ReadAsync();

        Assert.AreEqual(Protected, entry.Unsupported);
    }

    private void Choose(GameChoices choices) => _bed.Choices.Set(_bed.PathOf("Game"), choices);

    private Task<GameEntry> ReadAsync() => _bed.ReadAsync(_bed.Game("Game", "Game"), Ct);
}
