namespace ThePlot.Core.ContentGeneration;

public static class TextNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return text.ToLowerInvariant();
    }
}
