using DataEntities;
using Microsoft.Extensions.VectorData;
using System.Text.Json.Serialization;

namespace SearchEntities
{
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
        public string ProductInformation { get; set; }

        [VectorStoreRecordVector(384, DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
