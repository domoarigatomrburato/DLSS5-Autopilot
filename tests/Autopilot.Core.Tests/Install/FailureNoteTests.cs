using System.Text.Json.Nodes;
using Autopilot.Core.Install;
using Autopilot.Core.Net;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.State;

namespace Autopilot.Core.Tests.Install;

/// <summary>Why the last install or uninstall stopped, kept in the game's per-game state until the next one starts.</summary>
[TestClass]
public sealed class FailureNoteTests(TestContext testContext)
{
    private static readonly FailureNote _note = new(
        new DateTimeOffset(2026, 11, 27, 10, 30, 0, TimeSpan.FromHours(1)),
        FailureOperation.Install,
        FailureKind.Network,
        new FailureCause(NetErrorKind.Timeout, "github.com"),
        "ReShade could not be downloaded: github.com did not answer in time.",
        FailureOutcome.RolledBack,
        RouteId.Native,
        "1.2.3",
        null);

    private static readonly FailureNote _diskFull = _note with
    {
        Kind = FailureKind.DiskFull,
        Cause = new FailureCause(Drives: [new DrivePlace(new DriveSpace("D:", 1024), DriveRoles.FailedWrite | DriveRoles.Game), new DrivePlace(new DriveSpace("C:", null), DriveRoles.Cache)]),
        Outcome = FailureOutcome.RollbackIncomplete,
    };

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public void A_kept_note_reads_back_and_clearing_it_leaves_none()
    {
        using StateBed bed = new();

        FailureNotes.Keep(bed.Store, bed.Game, _note, Ct);

        Assert.AreEqual(_note, FailureNotes.Read(bed.NewStore(), bed.Game));
        FailureNotes.Clear(bed.Store, bed.Game, Ct);
        Assert.IsNull(FailureNotes.Read(bed.NewStore(), bed.Game));
    }

    [TestMethod]
    public void A_game_with_no_note_reads_none()
    {
        using StateBed bed = new();

        Assert.IsNull(FailureNotes.Read(bed.Store, bed.Game));
    }

    [TestMethod]
    public void A_full_drive_note_keeps_the_drives_and_their_free_space()
    {
        using StateBed bed = new();

        FailureNotes.Keep(bed.Store, bed.Game, _diskFull, Ct);
        FailureNote read = FailureNotes.Read(bed.NewStore(), bed.Game)!;

        CollectionAssert.AreEqual(_diskFull.Cause!.Drives!.ToList(), read.Cause!.Drives!.ToList());
        Assert.AreEqual("failed-write, game", (string?)bed.OnDisk()[FailureNotes.Key]!["cause"]!["drives"]![0]!["roles"]);
        Assert.AreEqual("rollback-incomplete", (string?)bed.OnDisk()[FailureNotes.Key]!["outcome"]);
    }

    [TestMethod]
    public void The_text_is_kept_to_400_characters_and_the_exception_to_its_last_4000()
    {
        using StateBed bed = new();
        string exception = "start of the trace " + new string('x', 5_000) + " the throw";

        FailureNotes.Keep(bed.Store, bed.Game, _note with { Kind = FailureKind.Unexpected, Text = new string('t', 450), Exception = exception }, Ct);
        FailureNote read = FailureNotes.Read(bed.Store, bed.Game)!;

        Assert.AreEqual(new string('t', 400), read.Text);
        Assert.AreEqual(exception[^4_000..], read.Exception);
        Assert.EndsWith(" the throw", read.Exception);
    }

    [TestMethod]
    public void A_cut_inside_a_character_made_of_two_halves_keeps_it_whole_or_leaves_it_out()
    {
        using StateBed bed = new();
        // The emoji's first half is the 400th character of the text, and its second half the first of the exception's last 4,000.
        string text = new string('t', 399) + "\U0001F600" + "after";
        string exception = new string('x', 10) + "\U0001F600" + new string('y', 3_999);

        FailureNotes.Keep(bed.Store, bed.Game, _note with { Text = text, Exception = exception }, Ct);
        FailureNote read = FailureNotes.Read(bed.Store, bed.Game)!;

        Assert.AreEqual(new string('t', 399), read.Text);
        Assert.AreEqual(new string('y', 3_999), read.Exception);
    }

    [TestMethod]
    [DataRow("5")]
    [DataRow("\"the install failed\"")]
    [DataRow(/*lang=json,strict*/ "{\"when\": \"2026-11-27T10:30:00+01:00\", \"operation\": 0, \"kind\": \"network\", \"cause\": null, \"text\": \"t\", \"outcome\": \"rolled-back\", \"route\": \"native\", \"toolVersion\": \"1\", \"exception\": null}")]
    [DataRow(/*lang=json,strict*/ "{\"when\": \"2026-11-27T10:30:00+01:00\", \"operation\": \"install\", \"kind\": \"network\", \"cause\": null, \"text\": \"t\", \"outcome\": \"rolled-back\", \"route\": \"nowhere\", \"toolVersion\": \"1\", \"exception\": null}")]
    [DataRow(/*lang=json,strict*/ "{\"operation\": \"install\"}")]
    public void A_value_that_is_not_a_note_reads_as_none(string value)
    {
        using StateBed bed = new();
        bed.Store.Update(bed.Game, state => state.Set("placeholder", 0, StateJson.Default.Int32), Ct);
        JsonObject file = bed.OnDisk();
        file[FailureNotes.Key] = JsonNode.Parse(value);
        bed.Plant(file.ToJsonString());

        Assert.IsNull(FailureNotes.Read(bed.NewStore(), bed.Game));
    }

    [TestMethod]
    public void Other_state_of_the_game_survives_keeping_and_clearing_a_note()
    {
        using StateBed bed = new();
        bed.Set("attempts", 3, Ct);

        FailureNotes.Keep(bed.Store, bed.Game, _note, Ct);
        FailureNotes.Clear(bed.Store, bed.Game, Ct);

        Assert.AreEqual(3, bed.Get("attempts"));
        Assert.IsFalse(bed.OnDisk().ContainsKey(FailureNotes.Key));
    }
}
