using Serilog;
using Serilog.Core;
using SmallShopBigAmbitions.Monads.Traceable;

namespace SmallShopBigAmbitions.Logic_examples;

public class TraceableIOLoggerExample
{
    public static void Main(string[] args)
    {
        // Instantiate a normal logger instance using LoggerFactory
        var logger = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(); // Add a console logger provider
        }).CreateLogger("TraceableExample");

        var result = EnrichUser("id123", logger).Run();

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

    private static IO<Option<EnrichedUserProfile>> EnrichUser(string user, Microsoft.Extensions.Logging.ILogger logger)
    {
        var tracedProfile = TraceableLifts.FromIO(
            MockDb.GetUserProfile(user),
            "user.profile.fetch",
            TraceableAttributes.FromResultOption<string>("profile")
        ).WithLogging(logger);

        var tracedBadge = TraceableLifts.FromIO(
            MockDb.GetUserProfileBadge(user),
            "user.badge.fetch",
            TraceableAttributes.FromResultOption<string>("badge")
        ).WithLogging(logger);

        var tracedExtra = TraceableLifts.FromIO(
            MockDb.GetMoreUserStuff(user),
            "user.extra.fetch",
            TraceableAttributes.FromResultOption<string>("extra")
        ).WithLogging(logger);

        return from p in IO.lift(() => tracedProfile.RunTraceable())
               from b in IO.lift(() => tracedBadge.RunTraceable())
               from e in IO.lift(() => tracedExtra.RunTraceable())
               let profileOpt = Flatten<string>(p)
               let badgeOpt = Flatten<string>(b)
               let extraOpt = Flatten<string>(e)
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
    /// <typeparam name="First">One of 3 options you want to bind</typeparam>
    /// <typeparam name="Second">One of 3 options you want to bind</typeparam>
    /// <typeparam name="Third">One of 3 options you want to bind</typeparam>
    /// <typeparam name="Return">Our return type. Doesnt have to be an Option</typeparam>
    /// <param name="f"></param>
    /// <param name="s"></param>
    /// <param name="t"></param>
    /// <param name="function">The function we will apply to all 3 Options with Map. Must be a function/delegate that handles the same types of First, Second, and Third, and that also returns the same </param>
    /// <returns></returns>
    private static Option<Return> Map3<First, Second, Third, Return>(
      Option<First> f,
      Option<Second> s,
      Option<Third> t,
      Func<First, Second, Third, Return> function)
    {
        return f.Bind(av =>
               s.Bind(bv =>
               t.Map(cv => function(av, bv, cv))));
    }
    public class ResultOpt<T>
    {
        public ResultOpt(Fin<Option<T>> value) => Value = value;

        public Fin<Option<T>> Value { get; }
    }
}

public record EnrichedUserProfile(
    string User,
    string Profile,
    string Badge,
    string Extra
);

// these should return their own types, not strings.
internal static partial class MockDb
{
    public static IO<Fin<Option<string>>> GetMoreUserStuff(string user) =>
        IO.lift<Fin<Option<string>>>(() => Fin<Option<string>>.Succ(Some($"Extra info for {user}")));

    public static IO<Fin<Option<string>>> GetUser() =>
            IO.lift<Fin<Option<string>>>(() => Fin<Option<string>>.Succ(Some("Alice")));

    public static IO<Fin<Option<string>>> GetUserProfile(string user) =>
        IO.lift<Fin<Option<string>>>(() => Fin<Option<string>>.Succ(Some($"Profile of {user}")));

    public static IO<Fin<Option<string>>> GetUserProfileBadge(string user) =>
        IO.lift<Fin<Option<string>>>(() => Fin<Option<string>>.Succ(Some($"Badge for {user}")));
}

// Example tracer source (replace with your actual tracer)
internal static class MyTracer
{
    // but an ActivitySource is a span in OTEL lingo? Dont we need more than this hardcoded one?
    public static readonly System.Diagnostics.ActivitySource Source = new("SmallShopBigAmbitions.Tracer");
}