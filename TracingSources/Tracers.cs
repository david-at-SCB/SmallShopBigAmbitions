using System.Diagnostics;

namespace SmallShopBigAmbitions.TracingSources;


public static class ShopActivitySource
{
    public static readonly ActivitySource Instance = new("SmallShopBigAmbitions");
}


public static class Telemetry
{
    public static readonly ActivitySource BillingServiceSource = new("Service.Billing");
    public static readonly ActivitySource CartServiceSource = new("Service.Cart");
    public static readonly ActivitySource OrderServiceSource = new("Service.Order");
    public static readonly ActivitySource MediatorServiceSource = new("Service.Mediator");
    public static readonly ActivitySource UserServiceSource = new("Service.User");
    public static readonly ActivitySource SiteWideServiceSource = new(SiteWideActivitySourceName);

    public const string SiteWideActivitySourceName = "SmallShopBigAmbitions";
}


public enum ServiceSpan
{
    Cart_GetCart,
    Billing_ChargeCustomer,
    Order_CreateOrder,
    Mediator_Dispatch,
    // etc.
}

public enum Services
{
    Cart,
    Billing,
    Order,
    User,
    Mediator,
    // etc.
}


