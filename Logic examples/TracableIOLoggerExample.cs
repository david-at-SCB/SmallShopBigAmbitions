using LanguageExt;
using LanguageExt.Pipes;
using LanguageExt.TypeClasses;
using Serilog;
using Serilog.Extensions.Logging;
using SmallShopBigAmbitions.Monads;
using static LanguageExt.Prelude;


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
        var tracedUser = TraceableLifts.FromAsyncEffect(
            MockDb.GetUser(),
            "user.fetch",
            TraceableAttributes.FromResultOption<string>("user.name")
        ).WithLogging(logger);

        // run the monad to get the user
        var userResult = await tracedUser.RunTraceableAsync();

        // now that we posssibly have a user, we can enrich it with more data. 
        // Local function that gets other optional data about the user. 
        async Task<Option<EnrichedUserProfile>> EnrichUser(string user)
        {
            var tracedProfile = TraceableLifts.FromAsyncEffect(
                MockDb.GetUserProfile(user),
                "user.profile.fetch",
                TraceableAttributes.FromResultOption<string>("profile")).WithLogging(logger);

            var tracedBadge = TraceableLifts.FromAff(
               MockDb.GetUserProfileBadge(user),
                "user.badge.fetch").WithLogging(logger);

            var tracedExtra = TraceableLifts.FromAff(
                MockDb.GetMoreUserStuff(user),
                "user.extra.fetch").WithLogging(logger);

            var profileTask = tracedProfile.RunTraceableAsync();
            var badgeTask = tracedBadge.RunTraceableAsync();
            var extraTask = tracedExtra.RunTraceableAsync();

            await Task.WhenAll(profileTask, badgeTask, extraTask);

            var profileOpt = Flatten(await profileTask);
            var badgeOpt = Flatten(await badgeTask);
            var extraOpt = Flatten(await extraTask);

            //var enrichedOpt = OptionExtensions.Map(
            //    Prelude.Some(user), profileOpt, badgeOpt, extraOpt,
            //    (u, p, b, e) => new EnrichedUserProfile(u, p, b, e)
            //);

            // instead of making a Monad stack like this with Bind:
            //return profileOpt.Bind(profile =>
            //    badgeOpt.Bind(badge =>
            //        extraOpt.Map(extra =>
            //            new EnrichedUserProfile(user, profile, badge, extra)
            //        )
            //    )
            //);

            // we wanna use a APplicative Functor like this:
            //var enrichedOpt =
            //    Apply<Option<string>, Option<string>, Option<string>, EnrichedUserProfile>.Apply(
            //        profileOpt,
            //        badgeOpt,
            //        extraOpt,
            //        (profile, badge, extra) => new EnrichedUserProfile(user, profile, badge, extra)
            //    );
            // that doesnt work with Option<T> so we have to use a custom Map3 function like this:
            var enrichedOpt = Map3(
            profileOpt,
            badgeOpt,
            extraOpt,
            (profile, badge, extra) => new EnrichedUserProfile(user, profile, badge, extra));
            if (enrichedOpt.IsNone)
            {
                logger.LogWarning("Failed to enrich user profile for user: {User}", user);
                return Option<EnrichedUserProfile>.None;
            }
            return enrichedOpt;
        }

        // Attempt to enrich the user profile
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


internal static class MockDb
{
    public static Aff<Fin<Option<string>>> GetUser() =>
        Aff(() => ValueTask.FromResult(Fin<Option<string>>.Succ(Some("Alice"))));

    public static Aff<Fin<Option<string>>> GetUserProfile(string user) =>
        Aff(() => ValueTask.FromResult(Fin<Option<string>>.Succ(Some($"Profile of {user}"))));

    public static Aff<Fin<Option<string>>> GetUserProfileBadge(string user) =>
        Aff(() => ValueTask.FromResult(Fin<Option<string>>.Succ(Some($"Badge for {user}"))));

    public static Aff<Fin<Option<string>>> GetMoreUserStuff(string user) =>
        Aff(() => ValueTask.FromResult(Fin<Option<string>>.Succ(Some($"Extra info for {user}"))));
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