using CondoAgents.Corpus;

namespace CondoAgents.Tests.Corpus;

public sealed class ParagraphChunkerTests
{
    [Fact]
    public void Numbers_pages_from_form_feeds()
    {
        var chunks = ParagraphChunker.Split("first\n\nsecond\fthird");

        Assert.Equal([1, 2], chunks.Select(c => c.Page).Distinct());
        Assert.Equal("third", chunks[^1].Text);
    }

    [Fact]
    public void Packs_paragraphs_up_to_the_target_without_splitting_one()
    {
        var longParagraph = new string('x', 900);
        var chunks = ParagraphChunker.Split($"a\n\nb\n\n{longParagraph}\n\nc", target: 10);

        Assert.Equal(["a\n\nb", longParagraph, "c"], chunks.Select(c => c.Text));
    }

    [Fact]
    public void Is_deterministic()
    {
        var text = File.ReadAllText(Path.Combine(TestData.Root, "buildings/jardins-plateau/bylaws-2024.fr.txt"));

        Assert.Equal(ParagraphChunker.Split(text), ParagraphChunker.Split(text));
    }
}