using System.Text;
using System.Text.Json;

namespace SqlServerAnalysisServices.Utility;

public class JsonPascalCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string value)
    {
        var builder = new StringBuilder();
        ReadOnlySpan<char> word;
        var pos = 0;
        while ((word = GetNextWord(value, ref pos)).Length != 0)
        {
            for (var i = 0; i < word.Length; i++)
            {
                char c;
                if (i == 0)
                {
                    c = char.ToUpperInvariant(word[i]);
                }
                else
                {
                    c = char.ToLowerInvariant(word[i]);
                }

                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///   Get the next 'word' from the string.
    /// </summary>
    /// <param name="value">The string to find words in.</param>
    /// <param name="pos">The current search index in value. This will be updated to the next search index when this function returns.</param>
    /// <remarks>
    ///   A 'word' is the next logical piece of a variable/property/parameter name
    /// </remarks>
    /// <returns>The 'word'</returns>
    private static ReadOnlySpan<char> GetNextWord(ReadOnlySpan<char> value, scoped ref int pos)
    {
        int? wordStart = null;
        for (int idx = pos; idx < value.Length; idx++)
        {
            if (wordStart.HasValue)
            {
                if (!char.IsLetterOrDigit(value[idx]) || char.IsUpper(value[idx]))
                {
                    // word is finished, update pos and return the word.
                    pos = idx;
                    return value[wordStart.Value..idx];
                }
            }
            else
            {
                // The first letter or digit found marks the start of the word.
                if (char.IsLetterOrDigit(value[idx]))
                {
                    wordStart = idx;
                }
            }
        }

        pos = value.Length;

        // We hit the end of the string, if we started a word return it.
        if (wordStart.HasValue)
        {
            return value[wordStart.Value..];
        }

        return value[..0];
    }
}