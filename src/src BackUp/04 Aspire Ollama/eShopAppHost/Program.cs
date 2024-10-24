var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama", port: 11434)
    .AddModel("phi3.5")
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
