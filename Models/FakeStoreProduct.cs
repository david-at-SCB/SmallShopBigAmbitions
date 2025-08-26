namespace SmallShopBigAmbitions.Models;

using System.Text.Json.Serialization;

public record FakeStoreRating(
    [property: JsonPropertyName("rate")] double Rate,
    [property: JsonPropertyName("count")] int Count
);

public record FakeStoreProduct(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("image")] string Image,
    [property: JsonPropertyName("rating")] Option<FakeStoreRating> Rating
);
