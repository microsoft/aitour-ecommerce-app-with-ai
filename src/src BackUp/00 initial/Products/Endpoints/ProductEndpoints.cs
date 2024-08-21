using SearchEntities;
using DataEntities;
using Microsoft.EntityFrameworkCore;
using Products.Data;
using System.Diagnostics;

namespace Products.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Product");

        group.MapGet("/", async (ProductDataContext db) =>
        {
            return await db.Product.ToListAsync();
        })
        .WithName("GetAllProducts")
        .Produces<List<Product>>(StatusCodes.Status200OK);

        group.MapGet("/{id}", async (int id, ProductDataContext db) =>
        {
            return await db.Product.AsNoTracking()
                .FirstOrDefaultAsync(model => model.Id == id)
                is Product model
                    ? Results.Ok(model)
                    : Results.NotFound();
        })
        .WithName("GetProductById")
        .Produces<Product>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id}", async (int id, Product product, ProductDataContext db) =>
        {
            var affected = await db.Product
                .Where(model => model.Id == id)
                .ExecuteUpdateAsync(setters => setters
                  .SetProperty(m => m.Id, product.Id)
                  .SetProperty(m => m.Name, product.Name)
                  .SetProperty(m => m.Description, product.Description)
                  .SetProperty(m => m.Price, product.Price)
                  .SetProperty(m => m.ImageUrl, product.ImageUrl)
                );

            return affected == 1 ? Results.Ok() : Results.NotFound();
        })
        .WithName("UpdateProduct")
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/", async (Product product, ProductDataContext db) =>
        {
            db.Product.Add(product);
            await db.SaveChangesAsync();
            return Results.Created($"/api/Product/{product.Id}", product);
        })
        .WithName("CreateProduct")
        .Produces<Product>(StatusCodes.Status201Created);

        group.MapDelete("/{id}", async (int id, ProductDataContext db) =>
        {
            var affected = await db.Product
                .Where(model => model.Id == id)
                .ExecuteDeleteAsync();

            return affected == 1 ? Results.Ok() : Results.NotFound();
        })
        .WithName("DeleteProduct")
        .Produces<Product>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/search/{search}", async (string search, ProductDataContext db) =>
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            List<Product> products = await db.Product
            .Where(p => EF.Functions.Like(p.Name, $"%{search}%"))
            .ToListAsync();

            stopwatch.Stop();

            var response = new SearchResponse();
            response.Products = products;
            response.Response = products.Count > 0 ?
                $"{products.Count} Products found for [{search}]" :
                $"No products found for [{search}]";
            response.ElapsedTime = stopwatch.Elapsed;
            return response;
        })
            .WithName("SearchAllProducts")
            .Produces<List<Product>>(StatusCodes.Status200OK);

        #region AI Search Endpoint
        //routes.MapGet("/api/aisearch/{search}",
        //    async (string search, ProductDataContext db, MemoryContext mc) =>
        //    {
        //        var result = await mc.Search(search, db);
        //        return Results.Ok(result);
        //    })
        //    .WithName("AISearch")
        //    .Produces<SearchResponse>(StatusCodes.Status200OK)
        //    .Produces(StatusCodes.Status404NotFound);
        #endregion
    }
}
