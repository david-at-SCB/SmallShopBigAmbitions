//using SmallShopBigAmbitions.Monads.Traceable;
//using LanguageExt.Effects;

//namespace SmallShopBigAmbitions.Logic_examples;

//public record Order(string Id, string UserId, decimal Amount);
//public record EnrichedOrder(Order Order, Option<string> ShippingInfo, Option<string> DiscountCode, Option<string> DeliveryEstimate);

//internal static partial class MockDb
//{
//    private static readonly Random Rng = new();

//    public static Aff<Fin<Seq<Order>>> GetRecentOrders(string userId) =>
//    SuccessAff(Fin<Seq<Order>>.Succ(
//        Enumerable.Range(1, Rng.Next(2, 5))
//            .Select(i => new Order(i.ToString(), userId, Rng.Next(50, 200)))
//            .ToSeq()
//    ));

//    public static Aff<Fin<Option<string>>> GetShippingInfo(string orderId) =>
//          SuccessAff(Fin<Option<string>>.Succ(
//              Rng.Next(0, 2) == 0 ? Option<string>.None : Some($"Shipping-{orderId}")
//          ));

//    public static Aff<Fin<Option<string>>> GetDiscountCode(string orderId) =>
//            SuccessAff(Fin<Option<string>>.Succ(
//                Rng.Next(0, 2) == 0 ? Option<string>.None : Some($"DISCOUNT-{orderId}")
//            ));

//    public static Aff<Fin<Option<string>>> GetDeliveryEstimate(string orderId) =>
//        SuccessAff(Fin<Option<string>>.Succ(
//            Rng.Next(0, 2) == 0 ? Option<string>.None : Some($"ETA-{orderId}")
//        ));
//}

//public class TracingSequence
//{
//    public async Task<Seq<EnrichedOrder>> GetRecentOrders_monadchaining(string userId, ILogger logger)
//    {
//        var tracedOrders = TraceableLifts.FromAffSeq(
//            MockDb.GetRecentOrders(userId),
//            "orders.fetch",
//            TraceableAttributes.FromResultSeq<Order>(OrdersAttributes)
//        ).WithLogging(logger);

//        var ordersResult = await tracedOrders.RunTraceable();

//        return await ordersResult.Match(
//            Succ: async orders =>
//            {
//                var enrichedOrders = await orders.Traverse(async order => // traverse is not defined for Seq?
//                {
//                    TraceableLifts.FromIO(
//                        MockDb.GetShippingInfo(order.Id).Map(Task.FromResult),
//                        "order.shipping.fetch"
//                        ).WithLogging(logger);

//                    var tracedDiscount = TraceableLifts.FromIO(
//                        new IO<Task<Fin<Option<string>>>>(() => Task.FromResult(MockDb.GetDiscountCode(order.Id).Run())),
//                        "order.discount.fetch"
//                    ).WithLogging(logger);

//                    var tracedEstimate = TraceableLifts.FromIO(
//                        new IO<Task<Fin<Option<string>>>>(() => Task.FromResult(MockDb.GetDeliveryEstimate(order.Id).Run())),
//                        "order.estimate.fetch"
//                    ).WithLogging(logger);

//                    var shipping = Flatten(await tracedShipping.RunTraceableAsync());
//                    var discount = Flatten(await tracedDiscount.RunTraceable());
//                    var estimate = Flatten(await tracedEstimate.RunTraceable());

//                    return new EnrichedOrder(order, shipping, discount, estimate);
//                });

//                return enrichedOrders.ToSeq();
//            },
//            Fail: err =>
//            {
//                logger.LogError("Failed to fetch orders: {Error}", err);
//                return Task.FromResult(LanguageExt.Seq<EnrichedOrder>.Empty); //CS8030 Anonymous function converted to a void returning delegate cannot return?
//            });
//    }

//    public Traceable<Fin<Seq<EnrichedOrder>>> GetRecentOrders_idiomatic(string userId, ILogger logger)
//    {
//        var enrichedOrdersAff =
//            from orders in MockDb.GetRecentOrders(userId)
//            from enriched in orders.TraverseParallel(order => // TraverseParallell isnt available in v4
//                from shipping in MockDb.GetShippingInfo(order.Id)
//                from discount in MockDb.GetDiscountCode(order.Id)
//                from estimate in MockDb.GetDeliveryEstimate(order.Id)
//                select new EnrichedOrder(
//                    order,
//                    shipping.IfFail(None),
//                    discount.IfFail(None),
//                    estimate.IfFail(None)
//                )
//            )
//            select enriched.ToSeq();

//        return TraceableLifts.FromAffSeq(
//            enrichedOrdersAff,
//            "orders.enriched",
//            EnrichedOrdersAttributes
//        ).WithLogging(logger);
//    }

//    public static IEnumerable<KeyValuePair<string, object>> OrdersAttributes(Seq<Order> orders) =>
//        new[]
//        {
//        new KeyValuePair<string, object>("orders.count", orders.Count),
//        new KeyValuePair<string, object>("orders.total_amount", orders.Sum(o => o.Amount)),
//        new KeyValuePair<string, object>("orders.user_ids", string.Join(",", orders.Map(o => o.UserId).Distinct()))
//        };

//    public static IEnumerable<KeyValuePair<string, object>> EnrichedOrdersAttributes(Seq<EnrichedOrder> enriched) =>
//        [
//        new KeyValuePair<string, object>("enriched.count", enriched.Count),
//        new KeyValuePair<string, object>("enriched.with_shipping", enriched.Count(e => e.ShippingInfo.IsSome)),
//        new KeyValuePair<string, object>("enriched.with_discount", enriched.Count(e => e.DiscountCode.IsSome)),
//        new KeyValuePair<string, object>("enriched.with_estimate", enriched.Count(e => e.DeliveryEstimate.IsSome))
//        ];

//    public Aff<Fin<Seq<EnrichedOrder>>> GetEnrichedOrders(string userId, ILogger logger)
//    {
//        return
//            from orders in MockDb.GetRecentOrders(userId)
//            from enriched in orders.TraverseParallel(order => // TraverseParallel is not available in v4
//                from shipping in MockDb.GetShippingInfo(order.Id)
//                from discount in MockDb.GetDiscountCode(order.Id)
//                from estimate in MockDb.GetDeliveryEstimate(order.Id)
//                select new EnrichedOrder(order, shipping.IfFail(None), discount.IfFail(None), estimate.IfFail(None))
//            )
//            select enriched.ToSeq(); // ToSeq() is not available in v4
//    }
//}

////✅ 2. Example Function Using Seq in a Webshop Context
////Let’s imagine a scenario where you want to fetch and enrich a sequence of recent orders for a user.Each order might have optional metadata like shipping info, discount code, and delivery estimate.You want to trace and log each step.

////This uses:

////Seq<T> to represent a sequence of orders.
////Traverse to apply an async enrichment function to each order.
////TraceableLifts and WithLogging to wrap each IO operation with observability.
////Flatten to unwrap Fin<Option<T>> into Option<T>.