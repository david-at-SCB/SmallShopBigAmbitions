using System.Diagnostics;

namespace SmallShopBigAmbitions.TracingSources;

public static class Telemetry
{
    public static readonly ActivitySource BillingSource = new("Service.Billing");
    public static readonly ActivitySource CartSource = new("Service.Cart");
    public static readonly ActivitySource OrderSource = new("Service.Order");
}
