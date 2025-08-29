namespace SmallShopBigAmbitions.Models;

public class CustomerViewModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    public List<Customer> Customers { get; set; } = [];
}

public record Customer(
    Guid Id,
    string? Name,
    string? Email,
    DiscountTier DiscountTier = DiscountTier.None
    );

public enum DiscountTier
{
    None,
    Bronze,
    Silver,
    Gold,
    Platinum,
    Tungsten
}