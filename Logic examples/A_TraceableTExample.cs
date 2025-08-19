using SmallShopBigAmbitions.Monads.Traceable;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using System.Security.Cryptography;

namespace SmallShopBigAmbitions.Logic_examples;

public static class C_ALittleMoreComplexTraceableTExample
{
    public static void Usage()
    {
        var logger = new LoggerFactory().CreateLogger("Webshop");

        int userId = 42;

        var result =
            from cart in CartService.GetCartItemsTraceable(userId, logger).RunTraceable()
            from order in OrderService.CreateOrderTraceable(cart, logger).RunTraceable()
            from billing in BillingService.ChargeCustomerTraceable(order, userId, logger).RunTraceable()
            select billing;

        bool success = result.Run();
        Console.WriteLine($"Billing success: {success}");

    }

    private static class BillingService
    {
        public static IO<bool> ChargeCustomer(string orderId, int userId) =>
            IO.lift(() =>
            {
                Thread.Sleep(120);
                return true; // Simulate success
            });

        public static TraceableT<bool> ChargeCustomerTraceable(string orderId, int userId, ILogger logger) =>
            new TraceableT<bool>(
                Effect: ChargeCustomer(orderId, userId),
                SpanName: "BillingService.ChargeCustomer",
                ActivitySource: TracerIOExample.Source,
                Attributes: success => new[]
                {
                new KeyValuePair<string, object>("order.id", orderId),
                new KeyValuePair<string, object>("user.id", userId),
                new KeyValuePair<string, object>("billing.success", success)
                }
            ).WithLogging(logger);
    }

    private static class CartService
    {
        public static IO<List<string>> GetCartItems(int userId) =>
            IO.lift(() =>
            {
                Thread.Sleep(100);
                return new List<string> { "Laptop", "Mouse", "Keyboard" };
            });

        public static TraceableT<List<string>> GetCartItemsTraceable(int userId, ILogger logger) =>
            new TraceableT<List<string>>(
                Effect: GetCartItems(userId),
                SpanName: "CartService.GetCartItems",
                ActivitySource: TracerIOExample.Source,
                Attributes: items => new[]
                {
                new KeyValuePair<string, object>("user.id", userId),
                new KeyValuePair<string, object>("cart.item.count", items.Count)
                }
            ).WithLogging(logger);
    }
    private static class OrderService
    {
        public static IO<string> CreateOrder(List<string> items) =>
            IO.lift(() =>
            {
                Thread.Sleep(150);
                return $"Order#{Guid.NewGuid()}";
            });

        public static TraceableT<string> CreateOrderTraceable(List<string> items, ILogger logger) =>
            new TraceableT<string>(
                Effect: CreateOrder(items),
                SpanName: "OrderService.CreateOrder",
                ActivitySource: TracerIOExample.Source,
                Attributes: orderId =>
                [
                new KeyValuePair<string, object>("order.id", orderId),
                new KeyValuePair<string, object>("order.item.count", items.Count)
                ]
            ).WithLogging(logger);
    }
}

public class A_TraceableTExample
{
    public static void Main()
    {
        // Instantiate a normal logger instance using LoggerFactory
        var logger = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(); // Add a console logger provider
        }).CreateLogger("TraceableExample");

        var result = EnrichUserAsyncVersion("id123", logger).Run();

        if (result.IsSome)
        {
            var enrichedProfile = result.Match(
                Some: profile => $"User: {profile.User}, Profile: {profile.Profile}, Badge: {profile.Badge}, Extra: {profile.Extra}",
                None: () => "No enriched profile found"
            );
            Console.WriteLine($"Enriched Profile: {enrichedProfile}");
        }
        else
        {
            Console.WriteLine("No profile found.");
        }
    }

    private static IO<Option<EnrichedUserProfile>> EnrichUserAsyncVersion(string user, Microsoft.Extensions.Logging.ILogger logger)
    {
        var tracedProfile = TraceableTLifts.FromIO(
              MockDb.GetUserProfile(user),
              "user.profile.fetch",
              TraceableTAttributes.FromFinOption<string>("profile")
          ).WithLogging(logger);

        var tracedBadge = TraceableTLifts.FromIO(
            MockDb.GetUserProfileBadge(user),
            "user.badge.fetch",
            TraceableTAttributes.FromFinOption<string>("badge")
        ).WithLogging(logger);

        var tracedExtra = TraceableTLifts.FromIO(
            MockDb.GetMoreUserStuff(user),
            "user.extra.fetch",
            TraceableTAttributes.FromFinOption<string>("extra")
        ).WithLogging(logger);

        // Fork each IO to run in parallel
        var pf = tracedProfile.RunTraceable().Fork();
        var bf = tracedBadge.RunTraceable().Fork();
        var ef = tracedExtra.RunTraceable().Fork();

        // Join them before entering LINQ
        var pIO = pf.Join(); // Join doesnt work without parameters. There is none that takes 0.
        var bIO = bf.Join();
        var eIO = ef.Join();


        
return
        from p in pIO
        from b in bIO
        from e in eIO
        let profileOpt = Flatten(p)
        let badgeOpt = Flatten(b)
        let extraOpt = Flatten(e)
               let enrichedOpt = Map3(profileOpt, badgeOpt, extraOpt,
                   (profile, badge, extra) => new EnrichedUserProfile(user, profile, badge, extra))
               select enrichedOpt;





    }

    /// <summary>
    /// Flatten a Fin<Option<T>> into an Option<T>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="result"></param>
    /// <returns></returns>
    private static Option<T> Flatten<T>(Fin<Option<T>> result) =>
    result.Match(
        Succ: opt => opt,
        Fail: _ => Option<T>.None
    );

    /// <summary>
    /// To avoid the need for a Monad stack, we can use a Map3 function to map over three Option values.
    /// We bind the first two Options and then with the third one we use Map to apply the function to all three values at once.
    /// </summary>
    /// <typeparam name="FirstType">One of 3 options you want to bind</typeparam>
    /// <typeparam name="SecondType">One of 3 options you want to bind</typeparam>
    /// <typeparam name="ThirdType">One of 3 options you want to bind</typeparam>
    /// <typeparam name="Return">Our return type. Doesnt have to be an Option</typeparam>
    /// <param name="f"></param>
    /// <param name="s"></param>
    /// <param name="t"></param>
    /// <param name="function">The function we will apply to all 3 Options with Map. Must be a function/delegate that handles the same types of First, Second, and Third, and that also returns the same </param>
    /// <returns></returns>
    private static Option<Return> Map3<FirstType, SecondType, ThirdType, Return>(
      Option<FirstType> f,
      Option<SecondType> s,
      Option<ThirdType> t,
      Func<FirstType, SecondType, ThirdType, Return> function)
    {
        return f.Bind(av =>
               s.Bind(bv =>
               t.Map(cv => function(av, bv, cv))));
    }
}

public class B_EvenMoreTraceableTExamples
{
    public static void Usage()
    {
        var traceable = UserService.FetchUserNameTraceable(42);
        var result = traceable.RunTraceable().Run(); // Run the IO<A> synchronously
        Console.WriteLine($"Fetched user: {result}");
    }

    private static class UserService
    {
        public static IO<string> FetchUserName(int userId) =>
            IO.lift(() =>
            {
                Thread.Sleep(100); // Simulate latency
                return $"User_{userId}";
            });

        public static TraceableT<string> FetchUserNameTraceable(int userId) =>
            new TraceableT<string>(
                Effect: FetchUserName(userId),
                SpanName: "FetchUserName",
                Attributes: name => new[]
                {
                    new KeyValuePair<string, object>("user.id", userId),
                    new KeyValuePair<string, object>("user.name", name)
                }
            );
    }
}
// Example tracer source (replace with your actual tracer)
internal static class TracerIOExample
{
    // but an ActivitySource is a span in OTEL lingo? Dont we need more than this hardcoded one?
    public static readonly System.Diagnostics.ActivitySource Source = new("SmallShopBigAmbitions.TraceableTExample");
}