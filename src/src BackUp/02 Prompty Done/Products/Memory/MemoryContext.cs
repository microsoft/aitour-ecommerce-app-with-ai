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
using System.ClientModel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace Products.Memory;

public class MemoryContext(
    ProductDataContext db,
    ITextEmbeddingGenerationService textEmbeddingGeneration,
    IVectorStoreRecordCollection<string, ProductVector> vectorStoreRecordCollection,
    Kernel kernel)
{
    public async Task FillProductsAsync()
    {
        await vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync();

        // get a copy of the list of products
        var products = await db.Product.ToListAsync();

        // iterate over the products and add them to the memory
        foreach (var product in products)
        {
            var productInfo = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]";

            var productVector = new ProductVector
            {
                Id = product.Id.ToString(),
                Name = product.Name,
                Description = product.Description,
                TextEmbedding = await textEmbeddingGeneration.GenerateEmbeddingAsync(productInfo)
            };

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

        if (firstResult is not null && firstResult.Score > 0.6)
        {
            // product found, search the db for the product details
            var prodId = firstResult.Record.Id;
            firstProduct = await db.Product.FindAsync(int.Parse(prodId));
            if (firstProduct is not null)
            {
                // let's improve the response message
                KernelArguments kernelArguments = new()
                {
                  { "productid", $"{firstProduct.Id}" },
                  { "productname", $"{firstProduct.Name}" },
                  { "productdescription", $"{firstProduct.Description}" },
                  { "productprice", $"{firstProduct.Price}" },
                  { "question", $"{search}" }
                };
                var prompty = kernel.CreateFunctionFromPromptyFile("aisearchresponse.prompty");
                responseText = await prompty.InvokeAsync<string>(kernel, kernelArguments);
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
        builder.Services.AddInMemoryVectorStoreRecordCollection<string, ProductVector>(MemoryCollectionName);
        builder.Services.AddVectorStoreTextSearch<ProductVector>();
        builder.Services.AddTransient<MemoryContext>();
    }

    public static void AddAzureAI(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(static sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            // get the configuration settings
            var endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? throw new ArgumentNullException(nameof(config), "AZURE_OPENAI_ENDPOINT is required.");
            var apiKey = config["AZURE_OPENAI_APIKEY"] ?? throw new ArgumentNullException(nameof(config), "AZURE_OPENAI_APIKEY is required.");

            // create the chat client
            return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        });

        builder.Services.AddSingleton<IChatCompletionService>(static sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var chatDeploymentName = config["AZURE_OPENAI_MODEL"] ?? throw new ArgumentNullException(nameof(config), "AZURE_OPENAI_MODEL is required.");

            var client = sp.GetRequiredService<AzureOpenAIClient>();
            return new AzureOpenAIChatCompletionService(chatDeploymentName, client);
        });

        builder.Services.AddSingleton<ITextEmbeddingGenerationService>(static sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var deploymentName = config["AZURE_OPENAI_EMBEDDING_MODEL"] ?? throw new ArgumentNullException(nameof(config), "AZURE_OPENAI_EMBEDDING_MODEL is required.");
            var client = sp.GetRequiredService<AzureOpenAIClient>();
            return new AzureOpenAITextEmbeddingGenerationService(deploymentName, client);
        });
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

public sealed class ProductVector
{
    [VectorStoreRecordKey]
    public required string Id { get; set; }

    [VectorStoreRecordData]
    public string? Name { get; set; }

    [VectorStoreRecordData]
    public string? Description { get; set; }

    [VectorStoreRecordVector(1536, DistanceFunction.CosineDistance)]
    public required ReadOnlyMemory<float> TextEmbedding { get; set; }
}