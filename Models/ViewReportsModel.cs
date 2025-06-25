namespace SmallShopBigAmbitions.Models;

public class ViewReportsModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    public List<Product> Products { get; set; } = [];
    public Content Contains { get; set; } = Models.Content.Unset;
}

public enum Content
{
    Unset,
    Text,
    Image,
    Video,
    Audio,
    EconReport,
    ObservabilityLog,
}