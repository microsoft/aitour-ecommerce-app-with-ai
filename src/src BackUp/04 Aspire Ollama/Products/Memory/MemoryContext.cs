#pragma warning disable SKEXP0020

using Microsoft.EntityFrameworkCore;
using SearchEntities;
using DataEntities;
using Products.Data;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.Extensions.VectorData;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Reflection.Emit;

namespace Products.Memory;

public class MemoryContext
{
    const string MemoryCollectionName = "products";

    private ILogger _logger;
    private IConfiguration _config;

    private IChatClient _chatClient;
    private IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    private List<ChatMessage> _messages;
    private IVectorStore _vectorStore;
    private IVectorStoreRecordCollection<int, ProductVector> _productsVector;

    public bool memoryStarted = false;

    public async Task InitMemoryContextAsync(ILogger logger, IConfiguration config, ProductDataContext db)
    {
        _logger = logger;

        // AI models
        _chatClient = new OllamaChatClient(new Uri("http://localhost:11434/"), "phi3.5");
        _embeddingGenerator = new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "all-minilm");

        // create vector store
        _vectorStore = new InMemoryVectorStore();
        _productsVector = _vectorStore.GetCollection<int, ProductVector>("products");
        await _productsVector.CreateCollectionIfNotExistsAsync();

        // create chat history
        InitChatHistory();
        FillProductsAsync(db);
    }

    private void InitChatHistory()
    {
        _messages =
        [
            new ChatMessage(ChatRole.System, "You are an AI assistant that helps people find information. Answer questions using a direct style and short answers. Do not share more information that the requested by the users."),
        ];
    }

    public async Task FillProductsAsync(ProductDataContext db)
    {
        // get a copy of the list of products
        var products = await db.Product.ToListAsync();

        // iterate over the products and add them to the memory
        foreach (var product in products)
        {
            var productInfo = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]";

            // create a product vector from the product
            var productVector = new ProductVector
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Description = product.Description,
                ProductInformation = productInfo,
                Vector = await _embeddingGenerator.GenerateEmbeddingVectorAsync(productInfo)
            };

            await _productsVector.UpsertAsync(productVector);
        }
    }

    public async Task<SearchResponse> Search(string search, ProductDataContext db)
    {
        var response = new SearchResponse();
        Product firstProduct = null;
        var responseText = "";

        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(search);

        var searchOptions = new VectorSearchOptions()
        {
            Top = 3,
            VectorPropertyName = "Vector"
        };

        var resultSearch = await _productsVector.VectorizedSearchAsync(queryEmbedding, searchOptions);

        await foreach (var result in resultSearch.Results)
        {
            if (result.Score > 0.4)
            {
                _logger.LogInformation($"Product found in memory. ProdId: {result.Record.Id}, Score: {result.Score}");
                // product found, search the db for the product details

                var productId = result?.Record?.Id;
                if (productId.HasValue)
                {
                    firstProduct = await db.Product.FindAsync(productId.Value);
                    responseText = $"The product [{firstProduct.Name}] fits with the search criteria [{search}][{result.Score?.ToString("0.00")}]";
                }
            }
        }

        if (firstProduct == null)
        {
            // no product found, ask the AI, keep the chat history
            _logger.LogInformation("Product not found in memory, asking the AI");
            InitChatHistory();
            _messages.Add(new ChatMessage(ChatRole.User, search));

            var result = _chatClient.CompleteAsync(chatMessages: _messages);
            responseText = result.Result.Message.ToString();
            _messages.Add(new ChatMessage(ChatRole.Assistant, responseText));

            _logger.LogInformation($"AI response: {responseText}");
        }
        else
        {
            _logger.LogInformation("Product found in memory. Building a user friendly response");

            try
            {
                // let's improve the response message
                var prompt = @$"You are an intelligent assistant helping Contoso Inc clients with their search about outdoor product. 
Generate a catchy and friendly message using the following information:
    - User Question: {search}
    - Found Product Name: {firstProduct.Name}
    - Found Product Id: {firstProduct.Id}
    - Found Product Price: {firstProduct.Price}
    - Found Product Description: {firstProduct.Description}
Include the found product information in the response to the user question.";

                _messages.Add(new ChatMessage(ChatRole.User, prompt));
                var result = _chatClient.CompleteAsync(chatMessages: _messages);
                responseText = result.Result.Message.ToString();

                _logger.LogInformation($"AI Response message: {responseText}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Error building the response message: {e.Message}");
            }

        }

        // create a response object
        return new SearchResponse
        {
            Products = firstProduct == null ? [new Product()] : [firstProduct],
            Response = responseText
        };
    }
}

public static class Extensions
{
    public static void InitSemanticMemory(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<MemoryContext>();
        context.InitMemoryContextAsync(
            services.GetRequiredService<ILogger<ProductDataContext>>(),
            services.GetRequiredService<IConfiguration>(),
            services.GetRequiredService<ProductDataContext>());
    }
}
