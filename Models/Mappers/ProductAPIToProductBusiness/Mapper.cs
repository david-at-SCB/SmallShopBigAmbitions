using LoggerMonadPlayground;
using static SmallShopBigAmbitions.Business.Services.ProductService;

namespace SmallShopBigAmbitions.Models.Mappers.ProductAPIToProductBusiness;

public static class Mapper
{
    public static FakeStoreProduct MapToBusinessProduct(ProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var rating = (dto.RatingCount > 0 || dto.RatingRate > 0)
                            ? Some(new FakeStoreRating(dto.RatingRate, dto.RatingCount))
                            : Option<FakeStoreRating>.None;

        return new FakeStoreProduct(
            dto.Id,
            dto.Title,
            dto.Price,
            dto.Description,
            dto.Category,
            dto.Image,
            rating
        );
    }
}
