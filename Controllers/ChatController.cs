using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace web2.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatClient _chatClient;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly SemanticSearch _semanticSearch;
        private readonly ReadCSV _readCSV;
        private readonly CsvToVector _csvToVector;

        private readonly PGVec _pGVec;

        public ChatController(
            IChatClient chatClient,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            SemanticSearch semanticSearch,
            ReadCSV readCSV,
            CsvToVector csvToVector,
            PGVec pGVec
            )
        {
            _chatClient = chatClient;
            _embeddingGenerator = embeddingGenerator;
            _semanticSearch = semanticSearch;
            _readCSV = readCSV;
            _csvToVector = csvToVector;
            _pGVec = pGVec;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<IEnumerable<string>>> Chat(string userMessage)
        {
            // var readCSV = new ReadCSV();
            var searchTool = AIFunctionFactory.Create(_semanticSearch.SearchForIssues);
            var aumDataTool = AIFunctionFactory.Create(_readCSV.AUMData);
            var getVectorTool = AIFunctionFactory.Create(_csvToVector.GenerateVector);
            var vecTool = AIFunctionFactory.Create(_pGVec.StoreVectorsInDB);
            var pgSearchTool = AIFunctionFactory.Create(_pGVec.SearchVectorsInDB);
            var getPGVecTool = AIFunctionFactory.Create(_pGVec.GenerateResponse);
            var chatOptions = new ChatOptions
            {
                Tools = new[]
            {
                searchTool,aumDataTool,getVectorTool,vecTool,pgSearchTool,getPGVecTool
                }
            };

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, """
                Hello!. You are Lumina.
                """),
                new(ChatRole.User, userMessage)
            };

            var response = _chatClient.CompleteAsync(messages, chatOptions);
            // var result = new List<string>();

            // await foreach (var chunk in response.Result.Message.Text())
            // {
            //     result.Add(chunk.Text);
            // }

            return Ok(response.Result.Message.Text);
        }
    }
}
