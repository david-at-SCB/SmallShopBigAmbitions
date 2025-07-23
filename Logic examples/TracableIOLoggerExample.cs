using LanguageExt;
using LanguageExt.Common;
using Serilog;
using Serilog.Extensions.Logging;
using SmallShopBigAmbitions.Monads;
using SmallShopBigAmbitions.Services;

namespace SmallShopBigAmbitions.Logic_examples;

public class TraceableIOLoggerExample
{
    public async Task<Fin<EnrichedUserProfile>> Example()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("App", "EnrichmentPipeline")
            .WriteTo.Console()
            .CreateLogger();

        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger<Program>();

        // package the IO operation into a traceable monad
        var tracedUser = TraceableLifts.FromIO(
            new IO<Task<Fin<Option<string>>>>(() => Task.FromResult(MockDb.GetUser().Run())),
            "user.fetch",
            TraceableAttributes.FromResultOption<string>("user.name")).WithLogging(logger);

        // run the monad to get the user
        var userResult = await tracedUser.RunTraceableAsync();

        // Local function that gets other optional data about the user
        async Task<Option<EnrichedUserProfile>> EnrichUser(string user)
        {
            var tracedProfile = TraceableLifts.FromIO(
                new IO<Task<Fin<Option<string>>>>(() => Task.FromResult(MockDb.GetUserProfile(user).Run())),
                "user.profile.fetch",
                TraceableAttributes.FromResultOption<string>("profile")).WithLogging(logger);

            var tracedBadge = TraceableLifts.FromIO(
                new IO<Task<Fin<Option<string>>>>(() => Task.FromResult(MockDb.GetUserProfileBadge(user).Run())),
                "user.badge.fetch").WithLogging(logger);

            var tracedExtra = TraceableLifts.FromIO(
                new IO<Task<Fin<Option<string>>>>(() => Task.FromResult(MockDb.GetMoreUserStuff(user).Run())),
                "user.extra.fetch").WithLogging(logger);

            var profileTask = tracedProfile.RunTraceableAsync();
            var badgeTask = tracedBadge.RunTraceableAsync();
            var extraTask = tracedExtra.RunTraceableAsync();

            await Task.WhenAll(profileTask, badgeTask, extraTask);

            var profileOpt = Flatten(await profileTask);
            var badgeOpt = Flatten(await badgeTask);
            var extraOpt = Flatten(await extraTask);

            var enrichedOpt = OptionExtensions.Map(
                Prelude.Some(user), profileOpt, badgeOpt, extraOpt,
                (u, p, b, e) => new EnrichedUserProfile(u, p, b, e)
            );
            if (enrichedOpt.IsNone)
            {
                logger.LogWarning("Failed to enrich user profile for user: {User}", user);
                return Option<EnrichedUserProfile>.None;
            }
            return profileOpt.Bind(profile =>
                badgeOpt.Bind(badge =>
                    extraOpt.Map(extra =>
                        new EnrichedUserProfile(user, profile, badge, extra)
                    )
                )
            );
        }
        var enrichedOption = await userResult.Match(
            Succ: async optUser =>
                await optUser.Match(
                    Some: EnrichUser,
                    None: () =>
                    {
                        logger.LogWarning("No user found.");
                        return Task.FromResult(Option<EnrichedUserProfile>.None);
                    }
                ),
            Fail: err =>
            {
                logger.LogError("Failed to fetch user: {Error}", err);
                return Task.FromResult(Option<EnrichedUserProfile>.None);
            }
        );

        var final = enrichedOption.Match( 
            Some: profile =>
            {
                logger.LogInformation("Successfully enriched user profile: {@Profile}", profile);
                return Fin<EnrichedUserProfile>.Succ(profile); 
            },
            None: () =>
            {
                logger.LogWarning("Missing optional data.");
                return Fin<EnrichedUserProfile>.Fail("Failed to enrich user profile.");
            }
        );
        return final;
       
    }
    public static Option<R> Map3<A, B, C, R>(
      Option<A> a,
      Option<B> b,
      Option<C> c,
      Func<A, B, C, R> f)
    {
        return a.Bind(av =>
               b.Bind(bv =>
               c.Map(cv => f(av, bv, cv))));
    }

    public static Option<T> Flatten<T>(Fin<Option<T>> result) =>
        result.Match(
            Succ: opt => opt,
            Fail: _ => Option<T>.None
        );
}

public record EnrichedUserProfile(
    string User,
    string Profile,
    string Badge,
    string Extra
);

internal static class MockDb
{
    public static IO<Fin<Option<string>>> GetUser() =>
        new(() => Fin<Option<string>>.Succ(Option<string>.Some("Alice")));

    public static IO<Fin<Option<string>>> GetUserProfile(string user) =>
        new(() => Fin<Option<string>>.Succ(Option<string>.Some($"Profile of {user}")));

    public static IO<Fin<Option<string>>> GetUserProfileBadge(string user) =>
        new(() => Fin<Option<string>>.Succ(Option<string>.Some($"Badge for {user}")));

    public static IO<Fin<Option<string>>> GetMoreUserStuff(string user) =>
        new(() => Fin<Option<string>>.Succ(Option<string>.Some($"Extra info for {user}")));
}

// Example tracer source (replace with your actual tracer)
internal static class MyTracer
{
    public static readonly System.Diagnostics.ActivitySource Source = new("SmallShopBigAmbitions.Tracer");
}


public static class OptionExtensions
{
    public static Option<R> Map<T1, T2, T3, T4, R>(
        Option<T1> o1, Option<T2> o2, Option<T3> o3, Option<T4> o4,
        Func<T1, T2, T3, T4, R> f) =>
        o1.Bind(t1 =>
        o2.Bind(t2 =>
        o3.Bind(t3 =>
        o4.Map(t4 => f(t1, t2, t3, t4)))));
}