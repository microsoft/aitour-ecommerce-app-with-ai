var builder = DistributedApplication.CreateBuilder(args);

var products = builder.AddProject<Projects.Products>("products");

if (builder.ExecutionContext.IsPublishMode)
{
    var ai = builder.AddAzureOpenAI("aoai")
        .AddDeployment(new AzureOpenAIDeployment("gpt4o", "gpt4o", "2024-08-06"))
        .AddDeployment(new AzureOpenAIDeployment("text-embedding-ada-002", "text-embedding-ada-002", "2"));

    products
        .WithReference(ai)
        .WithEnvironment("AZURE_OPENAI_MODEL", "gpt4o")
        .WithEnvironment("AZURE_OPENAI_EMBEDDING_MODEL", "text-embedding-ada-002");
}
else
{
    var ollama = builder.AddOllama("ollama")
        .WithDataVolume()
        .WithGPUSupport()
        .WithOpenWebUI();

    var chat = ollama.AddModel("chat", "phi3.5");
    var embedding = ollama.AddModel("embedding", "all-minilm");

    products
        .WithReference(chat)
        .WithReference(embedding)
        .WaitFor(chat)
        .WaitFor(embedding);
}

var store = builder.AddProject<Projects.Store>("store")
        .WithReference(products)
        .WithExternalHttpEndpoints()
        .WaitFor(products);

builder.Build().Run();
