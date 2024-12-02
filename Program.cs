using DotNetEnv;
using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qdrant.Client;

Env.Load(".env");
string githubKey = Env.GetString("GITHUB_KEY");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add the chat client
IChatClient innerChatClient = new ChatCompletionsClient(
    endpoint: new Uri("https://models.inference.ai.azure.com"),
    new AzureKeyCredential(githubKey))
    .AsChatClient("gpt-4o-mini");

builder.Services.AddChatClient(chatClientBuilder => chatClientBuilder
    .UseFunctionInvocation()
    // .UseLogging()
    .Use(innerChatClient));

// Register embedding generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    new AzureOpenAIClient(new Uri("https://models.inference.ai.azure.com"),
        new AzureKeyCredential(githubKey))
        .AsEmbeddingGenerator(modelId: "text-embedding-3-large"));

builder.Services.AddLogging(loggingBuilder =>
    loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Add QdrantClient and SemanticSearch service
builder.Services.AddSingleton(new QdrantClient("localhost"));
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddSingleton<ReadCSV>();
builder.Services.AddSingleton<CsvToVector>();
builder.Services.AddSingleton<PGVec>();
// var readCSV=new ReadCSV();
// var aumDataTool = AIFunctionFactory.Create(readCSV.AUMData);

var app = builder.Build();
// var chatClient = app.Services.GetRequiredService<IChatClient>();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
