using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Storage;

/// <summary>A file the tool writes whole and versions reads as missing, present, unreadable for now, damaged or newer, and nothing else.</summary>
[TestClass]
public sealed class VersionedJsonTests
{
    private const string Version = "schema";

    [TestMethod]
    public void A_missing_file_or_folder_is_missing()
    {
        using GameTree data = new("data");

        Assert.AreEqual(JsonFileState.Missing, Read(data.PathOf("a.json")).State);
        Assert.AreEqual(JsonFileState.Missing, Read(data.PathOf(@"games\a.json")).State);
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ """{"schema": 1, "a": 2}""", DisplayName = "the known version")]
    [DataRow(/*lang=json,strict*/ """{"a": 2}""", DisplayName = "no version")]
    public void A_file_of_a_version_this_build_knows_is_present_with_its_keys(string json)
    {
        using GameTree data = new GameTree("data").Add(TreeFile.Text("a.json", json));

        VersionedRead read = Read(data.PathOf("a.json"));

        Assert.AreEqual(JsonFileState.Present, read.State);
        Assert.AreEqual(2, (int)read.Document!["a"]!);
    }

    [TestMethod]
    public void A_file_of_a_higher_version_is_newer_and_still_read()
    {
        using GameTree data = new GameTree("data").Add(TreeFile.Text("a.json", /*lang=json,strict*/ """{"schema": 2, "a": 2}"""));

        VersionedRead read = Read(data.PathOf("a.json"));

        Assert.AreEqual(JsonFileState.Newer, read.State);
        Assert.AreEqual(2, (int)read.Document!["a"]!);
    }

    [TestMethod]
    [DataRow("", DisplayName = "an empty file")]
    [DataRow("{not json", DisplayName = "not JSON")]
    [DataRow("[1, 2]", DisplayName = "an array")]
    [DataRow(/*lang=json*/ """{"a": 1, "a": 2}""", DisplayName = "a key twice")]
    [DataRow(/*lang=json,strict*/ """{"schema": "2"}""", DisplayName = "a version that is a string")]
    [DataRow(/*lang=json,strict*/ """{"schema": 2.0}""", DisplayName = "a version that is not an integer")]
    [DataRow(/*lang=json,strict*/ """{"schema": null}""", DisplayName = "a null version")]
    [DataRow(/*lang=json,strict*/ """{"schema": 1, "pad": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"}""", DisplayName = "past the size limit")]
    public void A_file_the_tool_did_not_write_this_way_is_damaged(string content)
    {
        using GameTree data = new GameTree("data").Add(TreeFile.Text("a.json", content));

        VersionedRead read = Read(data.PathOf("a.json"));

        Assert.AreEqual(JsonFileState.Damaged, read.State);
        Assert.IsNull(read.Document);
    }

    [TestMethod]
    public void A_folder_at_the_path_is_damaged()
    {
        using GameTree data = new GameTree("data").Folder("a.json");

        Assert.AreEqual(JsonFileState.Damaged, Read(data.PathOf("a.json")).State);
    }

    [TestMethod]
    public void A_file_another_handle_holds_is_unreadable()
    {
        using GameTree data = new GameTree("data").Add(TreeFile.Text("a.json", /*lang=json,strict*/ """{"schema": 1}"""));
        using FileStream antivirus = new(data.PathOf("a.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        VersionedRead read = Read(data.PathOf("a.json"));

        Assert.AreEqual(JsonFileState.Unreadable, read.State);
        Assert.IsNull(read.Document);
    }

    private static VersionedRead Read(string path) => VersionedJson.Read(path, maxBytes: 100, Version, knownVersion: 1);
}
