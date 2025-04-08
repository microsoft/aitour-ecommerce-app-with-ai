var builder = DistributedApplication.CreateBuilder(args);

var ai = !builder.ExecutionContext.IsPublishMode ?
    builder.AddConnectionString("aoai") :
    builder.AddAzureOpenAI("aoai")
        .AddDeployment(new AzureOpenAIDeployment("gpt4o", "gpt4o", "2024-08-06"))
        .AddDeployment(new AzureOpenAIDeployment("text-embedding-3-large", "text-embedding-3-large", "1"));

var products = builder.AddProject<Projects.Products>("products")
    .WithReference(ai)
    .WithEnvironment("AZURE_OPENAI_MODEL", builder.Configuration["AZURE_OPENAI_MODEL"])
    .WithEnvironment("AZURE_OPENAI_EMBEDDING_MODEL", builder.Configuration["AZURE_OPENAI_EMBEDDING_MODEL"]);

var store = builder.AddProject<Projects.Store>("store")
    .WithReference(products)
    .WithExternalHttpEndpoints();

builder.Build().Run();
