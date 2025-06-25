namespace SmallShopBigAmbitions.Models;

public class CustomerViewModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    public List<Customer> Customers { get; set; } = [];
}

public record Customer
{
    private Guid Id { get; init; } = Guid.NewGuid();
    public string? Name { get; init; }
    public string? Email { get; init; }
    public DiscountTier DiscountTier { get; init; } = DiscountTier.None;
}

public enum DiscountTier
{
    None,
    Bronze,
    SIlver,
    Gold,
    Platinum,
    Tungsten
}
