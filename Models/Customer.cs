namespace SmallShopBigAmbitions.Models;

// Abstract base for customer identity
public abstract record CustomerId(Guid Id)
{
    public abstract bool IsRegistered { get; }
}

// Registered customer: always has a Guid and is registered
public record RegisteredCustomerId(Guid Id) : CustomerId(Id)
{
    public override bool IsRegistered => true;
}

// Guest customer: has a Guid and an email, not registered
public record GuestCustomerId(Guid Id, string Email) : CustomerId(Id)
{
    public override bool IsRegistered => false;
}

// Main customer record
public record Customer(
    CustomerId Id,
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

// Example view model for Razor Pages
public class CustomerViewModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    public List<Customer> Customers { get; set; } = [];
}