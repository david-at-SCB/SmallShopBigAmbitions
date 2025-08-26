using System.Diagnostics;

namespace SmallShopBigAmbitions.TracingSources;


public static class ShopActivitySource
{
    public static readonly ActivitySource Instance = new("SmallShopBigAmbitions");
}


public static class Telemetry
{
    public static readonly ActivitySource BillingSource = new("Service.Billing");
    public static readonly ActivitySource CartSource = new("Service.Cart");
    public static readonly ActivitySource OrderSource = new("Service.Order");
    public static readonly ActivitySource MediatorSource = new("Service.Mediator");
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


