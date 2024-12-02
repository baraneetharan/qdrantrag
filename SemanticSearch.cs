using Microsoft.Extensions.AI;
using Qdrant.Client;

public class SemanticSearch
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly QdrantClient _qdrantClient;

    public SemanticSearch(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, QdrantClient qdrantClient)
    {
        this._embeddingGenerator = embeddingGenerator;
        this._qdrantClient = qdrantClient;
    }

    public async Task<string> SearchForIssues(string searchPhrase)
    {
        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(searchPhrase);
        Console.WriteLine("embeddings.Count" + queryEmbedding.Length);
        Console.WriteLine("embeddings.Count" + queryEmbedding.ToArray().Length);

        var searchResults = await _qdrantClient.SearchAsync("issues", queryEmbedding, limit: 5);

        return string.Join(Environment.NewLine, searchResults.Select(r => $"<issue id='{r.Id}'>{r.Score}</issue>"));
    }
}
