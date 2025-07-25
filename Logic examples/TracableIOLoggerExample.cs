using SmallShopBigAmbitions.Monads;

namespace SmallShopBigAmbitions.Logic_examples;

public class TraceableIOLoggerExample
{
    IO<Option<EnrichedUserProfile>> EnrichUser(string user, ILogger logger)
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

        var forkProfile = IO.fork(tracedProfile.RunTraceable());
        var forkBadge = IO.fork(tracedBadge.RunTraceable());
        var forkExtra = IO.fork(tracedExtra.RunTraceable());

        return from p in forkProfile.Await
               from b in forkBadge.Await
               from e in forkExtra.Await
               let profileOpt = Flatten(p.Value)
               let badgeOpt = Flatten(b.Value)
               let extraOpt = Flatten(e.Value)
               let enrichedOpt = Map3(profileOpt, badgeOpt, extraOpt,
                   (profile, badge, extra) => new EnrichedUserProfile(user, profile, badge, extra))
               select enrichedOpt;
    }

    public class ResultOpt<T>
    {
        public Fin<Option<T>> Value { get; }

        public ResultOpt(Fin<Option<T>> value) => Value = value;
    }

    /// <summary>
    /// Flatten a Fin<Option<T>> into an Option<T>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="result"></param>
    /// <returns></returns>
    public static Option<T> Flatten<T>(Fin<Option<T>> result) =>
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
    public static Option<Return> Map3<First, Second, Third, Return>(
      Option<First> f,
      Option<Second> s,
      Option<Third> t,
      Func<First, Second, Third, Return> function)
    {
        return f.Bind(av =>
               s.Bind(bv =>
               t.Map(cv => function(av, bv, cv))));
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
    public static IO<Fin<Option<string>>> GetUser() =>
        IO.lift(() => Fin<Option<string>>.Succ(Some("Alice")));

    public static IO<Fin<Option<string>>> GetUserProfile(string user) =>
        IO.lift(() => Fin<Option<string>>.Succ(Some($"Profile of {user}")))


 public static IO<Fin<Option<string>>> GetUserProfileBadge(string user) =>
    IO.lift(() => Fin<Option<string>>.Succ(Some($"Badge for {user}")));

    public static IO<Fin<Option<string>>> GetMoreUserStuff(string user) =>
        IO.lift(() => Fin<Option<string>>.Succ(Some($"Extra info for {user}")));
}

// Example tracer source (replace with your actual tracer)
internal static class MyTracer
{
    public static readonly System.Diagnostics.ActivitySource Source = new("SmallShopBigAmbitions.Tracer");
}