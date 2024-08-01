using System.Text.Json.Serialization;

namespace DataEntities;

public class Product
{

    public Product()
    {
        Id = 0;
        Name = "not defined";
        Description = "not defined";
        Price = 0;
        ImageUrl = "not defined";
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}


[JsonSerializable(typeof(List<Product>))]
public sealed partial class ProductSerializerContext : JsonSerializerContext
{
}