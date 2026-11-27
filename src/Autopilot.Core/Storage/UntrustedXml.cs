using System.Xml;

namespace Autopilot.Core.Storage;

/// <summary>Reader settings for XML that is untrusted input.</summary>
internal static class UntrustedXml
{
    /// <summary>No DTD, no external entities, and at most <paramref name="maxCharacters"/> characters.</summary>
    public static XmlReaderSettings Settings(long maxCharacters, bool async = false) =>
        new() { Async = async, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = maxCharacters };
}
