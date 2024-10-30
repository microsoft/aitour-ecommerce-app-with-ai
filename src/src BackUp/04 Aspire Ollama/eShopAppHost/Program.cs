var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama", port: 11434)
    .AddModel("phi3.5")
    .AddModel("llama3.2")
    .AddModel("all-minilm")
    .WithDefaultModel("phi3.5")
    .WithDataVolume()
    .WithContainerRuntimeArgs("--gpus=all")
    .WithOpenWebUI();

var products = builder.AddProject<Projects.Products>("products")
    .WithReference(ollama);

var store = builder.AddProject<Projects.Store>("store")
    .WithReference(products)
    .WithExternalHttpEndpoints();

builder.Build().Run();
