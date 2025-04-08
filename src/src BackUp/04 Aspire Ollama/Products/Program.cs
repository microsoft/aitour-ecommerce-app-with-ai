using Microsoft.EntityFrameworkCore;
using Products.Data;
using Products.Endpoints;
using Products.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<ProductDataContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ProductsContext") ?? throw new InvalidOperationException("Connection string 'ProductsContext' not found.")));

builder.Services.AddSingleton<IConfiguration>(sp =>
{
    return builder.Configuration;
});

// add memory context
builder.AddSemanticKernel();

if (builder.Environment.IsDevelopment())
{
    builder.AddOllama();
}
else
{
    builder.AddAzureAI();
}

// Add services to the container.
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapProductEndpoints();

app.UseStaticFiles();

app.CreateDbIfNotExists();

// init semantic memory
await app.InitSemanticMemoryAsync();

app.MapDefaultEndpoints();

app.Run();
