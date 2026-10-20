namespace CondoAgents.Corpus;

/// <summary>Pages split on form feed, paragraphs on blank lines; a paragraph longer than the target stays whole so a table row is never cut.</summary>
public static class ParagraphChunker
{
    public const int TargetCharacters = 700;

    public static IReadOnlyList<Chunk> Split(string text, int target = TargetCharacters)
    {
        var chunks = new List<Chunk>();
        var pages = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\f');
        for (var p = 0; p < pages.Length; p++)
        {
            var paragraphs = pages[p]
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var buffer = new List<string>();
            var length = 0;
            foreach (var paragraph in paragraphs)
            {
                if (buffer.Count > 0 && length + paragraph.Length > target)
                {
                    chunks.Add(new Chunk(p + 1, string.Join("\n\n", buffer)));
                    buffer.Clear();
                    length = 0;
                }

                buffer.Add(paragraph);
                length += paragraph.Length + 2;
            }

            if (buffer.Count > 0)
            {
                chunks.Add(new Chunk(p + 1, string.Join("\n\n", buffer)));
            }
        }

        return chunks;
    }
}

public sealed record Chunk(int Page, string Text);