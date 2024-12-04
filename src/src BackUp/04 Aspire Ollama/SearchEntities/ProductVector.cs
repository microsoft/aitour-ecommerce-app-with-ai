using DataEntities;
using Microsoft.Extensions.VectorData;
using System.Text.Json.Serialization;

namespace SearchEntities;

public class ProductVector : Product
{
    [VectorStoreRecordKey]
    [JsonPropertyName("id")]
    public override int Id { get => base.Id ; set => base.Id = value; }

    [VectorStoreRecordData]
    [JsonPropertyName("name")]
    public override string? Name { get => base.Name ; set => base.Name = value; }

    [VectorStoreRecordData]
    [JsonPropertyName("price")]
    public override decimal Price { get => base.Price; set => base.Price = value; }

    [VectorStoreRecordData]
    [JsonPropertyName("imageUrl")]
    public override string? ImageUrl { get => base.ImageUrl; set => base.ImageUrl = value; }

    [VectorStoreRecordData]
    [JsonPropertyName("description")]
    public override string? Description { get => base.Description   ; set => base.Description = value; }

    [VectorStoreRecordData]
    public required string ProductInformation { get; set; }

    [VectorStoreRecordVector(384, DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float> Vector { get; set; }

    public static ProductVector FromProduct(Product product)
    {
        return new ProductVector
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            ImageUrl = product.ImageUrl,
            ProductInformation = $"[{product.Name}] is a product that costs [{product.Price}] and is described as [{product.Description}]",
        };
    }
}
