using System.Globalization;
using CsvHelper;
using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Google.Protobuf.WellKnownTypes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class CsvToVector
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly QdrantClient _qdrantClient;

    public CsvToVector(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, QdrantClient qdrantClient)
    {
        this._embeddingGenerator = embeddingGenerator;
        this._qdrantClient = qdrantClient;
    }

    public async Task<string> GenerateVector(string searchPhrase)
    {
        Console.WriteLine("Get CSV Data as String ...");
        string pathToCsvFile = @"aum.csv";
        List<AumRecord> records = null;

        try
        {
            using var reader = new StreamReader(pathToCsvFile);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            records = csv.GetRecords<AumRecord>().ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading CSV: {ex.Message}");
            return null;
        }

        // Convert records to embeddings
        var embeddingTasks = records.Select(record =>
            _embeddingGenerator.GenerateEmbeddingAsync(record.ClientName + " " + record.AssetType));
        var embeddings = await Task.WhenAll(embeddingTasks);

        // Create points
        var points = records.Select((record, index) => new PointStruct
        {
            Id = new PointId { Uuid = index.ToString() }, // Properly use PointId with Uuid
            Vectors = embeddings[index].Vector.ToArray(),
            Payload =
            {
                { "ClientID", record.ClientID },
                { "ClientName", record.ClientName },
                { "TotalAUM", (double)record.TotalAUM },
                { "AssetType", record.AssetType },
                { "Allocation", record.Allocation },
                { "AnnualFees", (double)record.AnnualFees }
            }
        }).ToList();

        // Upsert points into Qdrant
        try
        {
            await _qdrantClient.UpsertAsync("aum", points);
        }
        catch (Exception ex)
        {
            
            Console.WriteLine($"Error reading CSV: {ex.Message}");
        }
        

        // Now search the searchPhrase
        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(searchPhrase);
        var searchResults = await _qdrantClient.SearchAsync("aum", queryEmbedding.ToArray(), limit: 5);

        return string.Join(Environment.NewLine, searchResults.Select(r => $"<issue id='{r.Id}'>{r.Score}</issue>"));
    }
}

public class AumRecord
{
    public int ClientID { get; set; }
    public string ClientName { get; set; }
    public decimal TotalAUM { get; set; }
    public string AssetType { get; set; }
    public double Allocation { get; set; }
    public decimal AnnualFees { get; set; }
}
