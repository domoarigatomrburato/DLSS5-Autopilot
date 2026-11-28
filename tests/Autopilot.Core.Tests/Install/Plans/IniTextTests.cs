using System.Text;
using Autopilot.Core.Install.Plans;

namespace Autopilot.Core.Tests.Install.Plans;

[TestClass]
public sealed class IniTextTests
{
    [TestMethod]
    public void A_key_set_in_a_section_that_has_it_changes_that_line_only() =>
        Assert.AreEqual("; mine\r\n[ADDON]\r\nAddonPath=.\\\r\nOther = 1\r\n", Edit("; mine\r\n[ADDON]\r\nAddonPath=C:\\old\r\nOther = 1\r\n", ini => ini.Set("ADDON", "AddonPath", ".\\")));

    [TestMethod]
    public void A_missing_key_goes_before_the_sections_trailing_blank_lines() =>
        Assert.AreEqual("[A]\nx=1\nk=v\n\n[B]\ny=2\n", Edit("[A]\nx=1\n\n[B]\ny=2\n", ini => ini.Set("A", "k", "v")));

    [TestMethod]
    public void A_missing_section_is_appended_after_a_blank_line_even_when_the_last_line_has_no_ending() =>
        Assert.AreEqual("[A]\nx=1\n\n[B]\nk=v\n", Edit("[A]\nx=1", ini => ini.Set("B", "k", "v")));

    [TestMethod]
    public void Set_if_absent_keeps_a_deliberate_value()
    {
        Assert.AreEqual("[S]\nk=0\n", Edit("[S]\nk=0\n", ini => ini.SetIfAbsent("s", "K", "1")));
        Assert.AreEqual("[S]\n; k=2\nk = 0\n", Edit("[S]\n; k=2\nk = 0\n", ini => ini.SetIfAbsent("S", "k", "1")));
    }

    [TestMethod]
    public void The_byte_order_mark_and_text_outside_UTF_8_survive()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF, .. "[A]\r\nx=1\r\n"u8];
        byte[] latin1 = [.. "[A]\r\nname=calf"u8, 0xE9, .. "\r\n"u8];

        byte[] edited = [0xEF, 0xBB, 0xBF, .. "[A]\r\nx=1\r\nk=v\r\n"u8];

        CollectionAssert.AreEqual(edited, IniText.Parse(bom).Set("A", "k", "v").ToBytes());
        CollectionAssert.AreEqual(latin1, IniText.Parse(latin1).SetIfAbsent("A", "name", "x").ToBytes());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void A_UTF_16_file_keeps_its_encoding_and_its_sections_are_found(bool littleEndian)
    {
        UnicodeEncoding utf16 = new(bigEndian: !littleEndian, byteOrderMark: true);
        byte[] file = [.. utf16.GetPreamble(), .. utf16.GetBytes("[ADDON]\r\nAddonPath=C:\\old\r\n")];

        byte[] edited = IniText.Parse(file).Set("ADDON", "AddonPath", ".\\").ToBytes();

        CollectionAssert.AreEqual((byte[])[.. utf16.GetPreamble(), .. utf16.GetBytes("[ADDON]\r\nAddonPath=.\\\r\n")], edited);
    }

    [TestMethod]
    public void A_new_file_uses_Windows_line_endings() =>
        Assert.AreEqual("[A]\r\nk=v\r\n", Encoding.UTF8.GetString(IniText.Parse(null).Set("A", "k", "v").ToBytes()));

    private static string Edit(string text, Func<IniText, IniText> edit) => Encoding.UTF8.GetString(edit(IniText.Parse(Encoding.UTF8.GetBytes(text))).ToBytes());
}
