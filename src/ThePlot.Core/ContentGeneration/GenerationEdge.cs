namespace ThePlot.Core.ContentGeneration;

public sealed class GenerationEdge
{
    private GenerationEdge()
    {
    }

    public Guid FromNodeId { get; private init; }
    public Guid ToNodeId { get; private init; }

    public GenerationNode? FromNode { get; private init; }
    public GenerationNode? ToNode { get; private init; }

    public static GenerationEdge Create(Guid fromNodeId, Guid toNodeId)
    {
        return new GenerationEdge
        {
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId
        };
    }
}
