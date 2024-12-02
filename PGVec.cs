using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Extensions.AI;
using Npgsql;

public class PGVec
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private List<AUM> records;

    public PGVec(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        this._embeddingGenerator = embeddingGenerator;
    }

    public async Task StoreVectorsInDB()
    {
        Console.WriteLine("Get CSV Data as String from StoreVectorsInDB...");
        try
        {
            string pathToCsvFile = @"D:\baraneetharan\myworks\SemanticKernel\sk31\aum.csv";
            using var reader = new StreamReader(pathToCsvFile);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            records = csv.GetRecords<AUM>().ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading CSV: {ex.Message}");
        }

        // Convert records to embeddings
        var embeddingTasks = records.Select(record =>
            _embeddingGenerator.GenerateEmbeddingAsync(record.ClientName + " " + record.AssetType));
        var embeddings = await Task.WhenAll(embeddingTasks);

        // Verify the embedding dimensions
        foreach (var embedding in embeddings)
        {
            if (embedding.Vector.Length != 3072)
            {
                throw new Exception($"Embedding size mismatch: expected 3072, got {embedding.Vector.Length}");
            }
        }

        using (var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=Kgisl@12345;Database=skrag"))
        {
            connection.Open();

            // Create table with PGVector column if not exists
            using (var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS aumvectors (id SERIAL PRIMARY KEY, client_id INT, client_name TEXT, total_aum DECIMAL, asset_type TEXT, allocation DOUBLE PRECISION, annual_fees DECIMAL, vector double precision[])", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Insert vectors into PostgreSQL
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var (record, index) in records.Select((value, index) => (value, index)))
                    {
                        using (var cmd = new NpgsqlCommand("INSERT INTO aumvectors (client_id, client_name, total_aum, asset_type, allocation, annual_fees, vector) VALUES (@client_id, @client_name, @total_aum, @asset_type, @allocation, @annual_fees, @vector)", connection))
                        {
                            cmd.Parameters.AddWithValue("client_id", record.ClientID);
                            cmd.Parameters.AddWithValue("client_name", record.ClientName);
                            cmd.Parameters.AddWithValue("total_aum", record.TotalAUM);
                            cmd.Parameters.AddWithValue("asset_type", record.AssetType);
                            cmd.Parameters.AddWithValue("allocation", record.Allocation);
                            cmd.Parameters.AddWithValue("annual_fees", record.AnnualFees);
                            cmd.Parameters.AddWithValue("vector", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Double, embeddings[index].Vector.ToArray());

                            Console.WriteLine($"INSERT INTO aumvectors (client_id, client_name, total_aum, asset_type, allocation, annual_fees, vector) VALUES ({record.ClientID}, '{record.ClientName}', {record.TotalAUM}, '{record.AssetType}', {record.Allocation}, {record.AnnualFees}, ARRAY[{string.Join(",", embeddings[index].Vector.ToArray())}])");

                            cmd.ExecuteNonQuery();
                            Console.WriteLine("Record inserted successfully.");
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing query: {ex.Message}");
                    transaction.Rollback();
                }
            }
        }
    }

    public async Task SearchVectorsInDB(string searchPhrase)
    {
        using (var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=Kgisl@12345;Database=skrag"))
        {
            connection.Open();

            // Ensure the vector extension is installed
            using (var checkCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector SCHEMA public VERSION '0.6.2'", connection))
            {
                checkCmd.ExecuteNonQuery();
            }

            // Generate embedding vector
            var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(searchPhrase);
            double[] queryVectorArray = Array.ConvertAll(queryEmbedding.Span.ToArray(), item => (double)item);

            // Formatted query string with the specific vector values
            var queryVectorString = string.Join(",", queryVectorArray);
            var cmdText = $@"
            SELECT id, client_name, total_aum, asset_type, allocation, annual_fees, 
            1 - (vector <=> '[{queryVectorString}]') AS cosine_similarity
            FROM aumvectors
            ORDER BY cosine_similarity DESC
            LIMIT 5;";

            using (var cmd = new NpgsqlCommand(cmdText, connection))
            {
                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetInt32(0);
                            var clientName = reader.GetString(1);
                            var totalAum = reader.GetDecimal(2);
                            var assetType = reader.GetString(3);
                            var allocation = reader.GetDouble(4);
                            var annualFees = reader.GetDecimal(5);
                            var cosineSimilarity = reader.GetDouble(6);

                            Console.WriteLine($"ID: {id}, ClientName: {clientName}, TotalAUM: {totalAum}, AssetType: {assetType}, Allocation: {allocation}, AnnualFees: {annualFees}, CosineSimilarity: {cosineSimilarity}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing query: {ex.Message}");
                }
            }
        }
    }

    public async Task<string> GenerateResponse(string searchPhrase)
    {
        using (var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=Kgisl@12345;Database=skrag"))
        {
            connection.Open();

            // Ensure the vector extension is installed
            using (var checkCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector SCHEMA public VERSION '0.6.2'", connection))
            {
                checkCmd.ExecuteNonQuery();
            }

            // Generate embedding vector
            var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(searchPhrase);
            double[] queryVectorArray = Array.ConvertAll(queryEmbedding.Span.ToArray(), item => (double)item);

            // Formatted query string with the specific vector values
            var queryVectorString = string.Join(",", queryVectorArray);
            var cmdText = $@"
                SELECT id, client_name, total_aum, asset_type, allocation, annual_fees, 
                1 - (vector <=> '[{queryVectorString}]') AS cosine_similarity
                FROM aumvectors
                ORDER BY cosine_similarity DESC
                LIMIT 5;";

            var response = "Here are the top results:\n";
            using (var cmd = new NpgsqlCommand(cmdText, connection))
            {
                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = reader.GetInt32(0);
                            var clientName = reader.GetString(1);
                            var totalAum = reader.GetDecimal(2);
                            var assetType = reader.GetString(3);
                            var allocation = reader.GetDouble(4);
                            var annualFees = reader.GetDecimal(5);
                            var cosineSimilarity = reader.GetDouble(6);

                            response += $"ID: {id}, ClientName: {clientName}, TotalAUM: {totalAum}, AssetType: {assetType}, Allocation: {allocation}, AnnualFees: {annualFees}, CosineSimilarity: {cosineSimilarity}\n";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing query: {ex.Message}");
                    return $"Error executing query: {ex.Message}";
                }
            }

            return response;
        }
    }
}
