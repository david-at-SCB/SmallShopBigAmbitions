using System.Diagnostics;

namespace SmallShopBigAmbitions.TracingSources;

public static class Telemetry
{
    public static readonly ActivitySource CartSource = new("SmallShop.Cart");
    public static readonly ActivitySource OrderSource = new("SmallShop.Order");
    public static readonly ActivitySource BillingSource = new("SmallShop.Billing");
}
