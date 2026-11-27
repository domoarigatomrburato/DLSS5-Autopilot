using System.Text.RegularExpressions;

namespace Autopilot.Core.Components;

/// <summary>How one version stands against another.</summary>
public enum VersionOrder
{
    /// <summary>Either side is not a version (<c>latest</c>, <c>nightly</c>): neither current nor behind.</summary>
    NotComparable,

    /// <summary>The labels differ and their numbers cannot say which is newer: a choice, never "older".</summary>
    Different,

    Older,

    Same,

    Newer,
}

/// <summary>
/// A component's version label, compared by its integers: the first four, so <c>310.8.0</c> is
/// newer than <c>310.7.9</c>, and a test build below its release (<c>v0.9.0-beta.1</c> before
/// <c>v0.9.0</c>). <c>latest</c> and <c>nightly</c> name no version and compare with nothing.
/// </summary>
public sealed partial class ComponentVersion
{
    private const int ComparedNumbers = 4;

    // The words a publisher marks a test build with in its tag (sources.py:253).
    private static readonly string[] _testWords = ["beta", "alpha", "-rc", "preview", "-pre"];
    private static readonly string[] _notVersions = ["latest", "nightly"];

    private readonly string[] _numbers;
    private readonly string[] _testNumbers;

    public ComponentVersion(string label)
    {
        Label = label;
        IsComparable = !_notVersions.Contains(label.Trim(), StringComparer.OrdinalIgnoreCase);
        int test = TestWordAt(label);
        IsTestBuild = test >= 0;
        _numbers = Numbers(IsTestBuild ? label[..test] : label);
        _testNumbers = IsTestBuild ? Numbers(label[test..]) : [];
    }

    /// <summary>The version recorded for a file taken from the <c>latest/download</c> redirect: whatever it pointed at.</summary>
    public static ComponentVersion Latest { get; } = new("latest");

    public string Label { get; }

    /// <summary>False for a label that names no version (<c>latest</c>, <c>nightly</c>), which is never compared.</summary>
    public bool IsComparable { get; }

    /// <summary>Whether the label carries a test-build word (<c>beta</c>, <c>alpha</c>, <c>-rc</c>, <c>preview</c>, <c>-pre</c>).</summary>
    public bool IsTestBuild { get; }

    /// <summary>How this version stands against <paramref name="other"/>.</summary>
    public VersionOrder CompareTo(ComponentVersion other)
    {
        if (!IsComparable || !other.IsComparable)
        {
            return VersionOrder.NotComparable;
        }

        if (_numbers.Length == 0 || other._numbers.Length == 0)
        {
            return string.Equals(Label.Trim(), other.Label.Trim(), StringComparison.OrdinalIgnoreCase) ? VersionOrder.Same : VersionOrder.Different;
        }

        int order = Compare(_numbers, other._numbers);
        if (order == 0)
        {
            // A release is newer than any test build of the same number.
            order = IsTestBuild == other.IsTestBuild ? Compare(_testNumbers, other._testNumbers) : IsTestBuild ? -1 : 1;
        }

        return order switch
        {
            < 0 => VersionOrder.Older,
            > 0 => VersionOrder.Newer,
            _ => VersionOrder.Same,
        };
    }

    public override string ToString() => Label;

    private static int TestWordAt(string label) =>
        _testWords.Select(word => label.IndexOf(word, StringComparison.OrdinalIgnoreCase)).Where(at => at >= 0).DefaultIfEmpty(-1).Min();

    // As digit strings without leading zeros, so a number of any length compares without overflow.
    private static string[] Numbers(string text) =>
        [.. Digits().Matches(text).Take(ComparedNumbers).Select(match => match.Value.TrimStart('0'))];

    // The first four integers, a missing one counting as zero.
    private static int Compare(string[] mine, string[] theirs)
    {
        for (int i = 0; i < ComparedNumbers; i++)
        {
            string a = i < mine.Length ? mine[i] : "";
            string b = i < theirs.Length ? theirs[i] : "";
            int order = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
            if (order != 0)
            {
                return Math.Sign(order);
            }
        }

        return 0;
    }

    [GeneratedRegex("[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();
}
