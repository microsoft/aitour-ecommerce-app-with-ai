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
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace Products.Memory;

public class MemoryContext(
    ProductDataContext db,
    ITextEmbeddingGenerationService textEmbeddingGeneration,
    IVectorStoreRecordCollection<int, ProductVector> vectorStoreRecordCollection,
    IChatCompletionService chatClient)
{
    private readonly ChatHistory _messages = [
            new ChatMessageContent(AuthorRole.System, "You are an AI assistant that helps people find information. Answer questions using a direct style and short answers. Do not share more information that the requested by the users."),
        ];

    public async Task FillProductsAsync()
    {
        await vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync();

        // get a copy of the list of products
        var products = await db.Product.ToListAsync();

        // iterate over the products and add them to the memory
        foreach (var product in products)
        {
            var productVector = ProductVector.FromProduct(product);
            productVector.Vector = await textEmbeddingGeneration.GenerateEmbeddingAsync(productVector.ProductInformation);

            await vectorStoreRecordCollection.UpsertAsync(productVector);
        }
    }

    public async Task<SearchResponse> Search(string search, ProductDataContext db)
    {
        Product? firstProduct = null;
        string? responseText = null;

        // search the vector database for the most similar product
        var searchVector = await textEmbeddingGeneration.GenerateEmbeddingAsync(search);
        var memorySearchResult = await vectorStoreRecordCollection.VectorizedSearchAsync(searchVector, new() { Top = 1 });

        var firstResult = await memorySearchResult.Results.FirstOrDefaultAsync();

        if (firstResult is not null && firstResult.Score > 0.5)
        {
            // product found, search the db for the product details
            var prodId = firstResult.Record.Id;
            firstProduct = await db.Product.FindAsync(prodId);
            if (firstProduct is not null)
            {
                var prompt = @$"You are an intelligent assistant helping Contoso Inc clients with their search about outdoor product.
Generate a catchy and friendly message using the following information:
    - User Question: {search}
    - Found Product Name: {firstProduct.Name}
    - Found Product Id: {firstProduct.Id}
    - Found Product Price: {firstProduct.Price}
    - Found Product Description: {firstProduct.Description}
Include the found product information in the response to the user question.";

                _messages.AddUserMessage(prompt);
                var result = await chatClient.GetChatMessageContentsAsync(_messages);
                responseText = result[^1].Content;
            }
        }

        // create a response object
        return new SearchResponse
        {
            Products = firstProduct is null ? [new Product()] : [firstProduct],
            Response = responseText
        };
    }
}

public static class MemoryContextExtensions
{
    const string MemoryCollectionName = "products";

    public static void AddSemanticKernel(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(static sp => new Kernel(sp));
        builder.Services.AddInMemoryVectorStoreRecordCollection<int, ProductVector>(MemoryCollectionName);
        builder.Services.AddVectorStoreTextSearch<ProductVector>();
        builder.Services.AddTransient<MemoryContext>();

        builder.Services.AddSingleton(static sp =>
            sp.GetRequiredKeyedService<IChatClient>("chat").AsChatCompletionService());

        builder.Services.AddSingleton(static sp =>
            sp.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("embedding").AsTextEmbeddingGenerationService());
    }

    public static void AddAzureAI(this WebApplicationBuilder builder)
    {
        builder.AddAzureOpenAIClient("aoai");
        builder.Services.AddKeyedSingleton("chat", static (sp, _) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var chatDeploymentName = config["AZURE_OPENAI_MODEL"] ?? throw new ArgumentNullException(nameof(config), "AZURE_OPENAI_MODEL is required.");

            return sp.GetRequiredService<AzureOpenAIClient>().AsChatClient(chatDeploymentName);
        });

        builder.Services.AddKeyedSingleton("embedding", static (sp, _) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var deploymentName = config["AZURE_OPENAI_EMBEDDING_MODEL"] ?? throw new ArgumentNullException(nameof(config), "AZURE_OPENAI_EMBEDDING_MODEL is required.");
            return sp.GetRequiredService<AzureOpenAIClient>().AsEmbeddingGenerator(deploymentName);
        });
    }

    public static void AddOllama(this WebApplicationBuilder builder)
    {
        builder.AddKeyedOllamaSharpChatClient("chat");
        builder.AddKeyedOllamaSharpEmbeddingGenerator("embedding");
    }

    public static async Task InitSemanticMemoryAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ProductDataContext>();
        var memory = services.GetRequiredService<MemoryContext>();
        await memory.FillProductsAsync();
    }
}
