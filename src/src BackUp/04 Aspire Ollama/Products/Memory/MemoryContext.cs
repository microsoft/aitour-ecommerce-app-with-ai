#pragma warning disable SKEXP0003
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0040
#pragma warning disable SKEXP0050
#pragma warning disable SKEXP0052

using Microsoft.EntityFrameworkCore;
using SearchEntities;
using DataEntities;
using Products.Data;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using Microsoft.SemanticKernel.Embeddings;
using Azure.AI.OpenAI;
using System.Diagnostics.Eventing.Reader;
using static Microsoft.KernelMemory.Constants.CustomContext;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.SemanticKernel.Plugins.Memory;

namespace Products.Memory;

public class MemoryContext
{
    const string MemoryCollectionName = "products";

    private ILogger _logger;
    private IConfiguration _config;
    private ChatHistory _chatHistory;
    public Kernel _kernel;
    private IChatCompletionService _chat;
    private ITextEmbeddingGenerationService _embeddingGenerator;
    public ISemanticTextMemory _memory;

    public bool memoryStarted = false;

    public void InitMemoryContext(ILogger logger, IConfiguration config, ProductDataContext db)
    {
        _logger = logger;

        // create kernel and add chat completion
        var modelId = "phi3.5";
        var builderSK = Kernel.CreateBuilder();
        builderSK.AddOpenAIChatCompletion(
            modelId: modelId,
            endpoint: new Uri("http://localhost:11434/"),
            apiKey: "apikey");
        builderSK.AddLocalTextEmbeddingGeneration();
        _kernel = builderSK.Build();

        _chat = _kernel.GetRequiredService<IChatCompletionService>();
        _embeddingGenerator = _kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();

        // create Semantic Memory
        //_memory = new SemanticTextMemory(new VolatileMemoryStore(), _embeddingGenerator);
        // create Semantic Memory
        var memoryBuilder = new MemoryBuilder();
        memoryBuilder.WithTextEmbeddingGeneration(_embeddingGenerator);
        memoryBuilder.WithMemoryStore(new VolatileMemoryStore());
        _memory = memoryBuilder.Build();

        // create chat history
        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage("You are a useful assistant. You always reply with a short and funny message. If you do not know an answer, you say 'I don't know that.' You only answer questions related to outdoor camping products. For any other type of questions, explain to the user that you only answer outdoor camping products questions. Do not store memory of the chat conversation.");

        FillProductsAsync(db);

        // Import the text memory plugin into the Kernel.
        TextMemoryPlugin memoryPlugin = new(_memory);
        _kernel.ImportPluginFromObject(memoryPlugin);
    }

    public async Task FillProductsAsync(ProductDataContext db)
    {
        // get a copy of the list of products
        var products = await db.Product.ToListAsync();

        // iterate over the products and add them to the memory
        foreach (var product in products)
        {
            var productInfo = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]";

            await _memory.SaveInformationAsync(
                collection: MemoryCollectionName,
                text: productInfo,
                id: product.Id.ToString(),
                description: product.Description,
                kernel: _kernel);
        }
    }

    public async Task<SearchResponse> Search(string search, ProductDataContext db)
    {
        var response = new SearchResponse();
        Product firstProduct = null;
        var responseText = "";

        // search the vector database for the most similar product        
        var memorySearchResult = await _memory.SearchAsync(MemoryCollectionName, search, withEmbeddings: true).FirstOrDefaultAsync();
        if (memorySearchResult != null && memorySearchResult.Relevance > 0.6)
        {
            _logger.LogInformation($"Product found in memory. ProdId: {memorySearchResult.Metadata.Id}, Relevance: {memorySearchResult.Relevance}");
            // product found, search the db for the product details
            var prodId = memorySearchResult.Metadata.Id;
            firstProduct = await db.Product.FindAsync(int.Parse(prodId));
            if (firstProduct != null)
            {
                responseText = $"The product [{firstProduct.Name}] fits with the search criteria [{search}][{memorySearchResult.Relevance.ToString("0.00")}]";
            }
        }

        if (firstProduct == null)
        {
            _logger.LogInformation("Product not found in memory, asking the AI");
            // no product found, ask the AI, keep the chat history
            _chatHistory.AddUserMessage(search);
            var result = await _chat.GetChatMessageContentsAsync(_chatHistory);
            responseText = result[^1].Content;
            _chatHistory.AddAssistantMessage(responseText);
            _logger.LogInformation($"AI response: {responseText}");
        }
        else
        {
            _logger.LogInformation("Product found in memory. Building a user friendly response");

            try
            {
                // let's improve the response message
                KernelArguments kernelArguments = new()
                            {
                              { "productid", $"{firstProduct.Id.ToString()}" },
                              { "productname", $"{firstProduct.Name}" },
                              { "productdescription", $"{firstProduct.Description}" },
                              { "productprice", $"{firstProduct.Price}" },
                              { "question", $"{search}" }
                            };
                var prompty = _kernel.CreateFunctionFromPromptyFile("aisearchresponse.prompty");
                responseText = await prompty.InvokeAsync<string>(_kernel, kernelArguments);
                _logger.LogInformation($"AI Prompty Response message: {responseText}");
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
        context.InitMemoryContext(
            services.GetRequiredService<ILogger<ProductDataContext>>(),
            services.GetRequiredService<IConfiguration>(),
            services.GetRequiredService<ProductDataContext>());
    }
}
