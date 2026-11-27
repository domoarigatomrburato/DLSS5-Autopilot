namespace Autopilot.Core.Library;

/// <summary>Finds a word in a name as a whole word, in any case: no letter or digit right before or after it.</summary>
internal static class WholeWord
{
    /// <summary>
    /// Whether <paramref name="word"/> is a whole word of <paramref name="text"/>; with
    /// <paramref name="digitsAfter"/>, digits right after it still count as part of it (<c>mhyprot3</c>).
    /// </summary>
    public static bool In(string text, string word, bool digitsAfter = false)
    {
        for (int at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            int end = at + word.Length;
            while (digitsAfter && end < text.Length && char.IsDigit(text[end]))
            {
                end++;
            }

            if (!IsWordCharacter(text, at - 1) && !IsWordCharacter(text, end))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordCharacter(string text, int index) => index >= 0 && index < text.Length && char.IsLetterOrDigit(text[index]);
}
