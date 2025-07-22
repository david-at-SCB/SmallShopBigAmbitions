using LanguageExt;
using LanguageExt.Common;
using Serilog;
using Serilog.Extensions.Logging;
using SmallShopBigAmbitions.Monads;
using System.Diagnostics;

namespace SmallShopBigAmbitions.Logic_examples;

internal class TracableIOLoggerExample
{
    public async void Example()
    {
        Log.Logger = new LoggerConfiguration()
        .Enrich.WithProperty("App", "EnrichmentPipeline")
        .WriteTo.Console()
        .CreateLogger();

        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger<Program>();

        var tracedUser = Traceable<IO<Result<Option<string>>>>.FromIO(
            MockDb.GetUser(),
            "user.fetch",
            user => new[] { KeyValuePair.Create("user.name", user) }
        );

        var userResult = tracedUser.Run().Run().Run()
            .Match(
                Succ: res => res,
                Fail: ex => Option<string>.None);

        var enrichedResult = await userResult.Match(
            Some: async user =>
            {
                var tracedProfile = Traceable<IO<Result<Option<string>>>>.FromIO(
                    MockDb.GetUserProfile(user),
                    "user.profile.fetch",
                    profile => new[] { KeyValuePair.Create("profile", profile) }
                );

                var tracedBadge = Traceable<IO<Result<Option<string>>>>.FromIO(
                    MockDb.GetUserProfileBadge(user),
                    "user.badge.fetch"
                );

                var tracedExtra = Traceable<IO<Result<Option<string>>>>.FromIO(
                    MockDb.GetMoreUserStuff(user),
                    "user.extra.fetch"
                );

                var profileTask = Task.Run(() => tracedProfile.Run().Run());
                var badgeTask = Task.Run(() => tracedBadge.Run().Run());
                var extraTask = Task.Run(() => tracedExtra.Run().Run());

                await Task.WhenAll(profileTask, badgeTask, extraTask);

                var profileOpt = profileTask.Result;
                var badgeOpt = badgeTask.Result;
                var extraOpt = extraTask.Result;

                return profileOpt.Bind(profile =>
                    badgeOpt.Bind(badge =>
                        extraOpt.Map(extra =>
                            new Option<EnrichedUserProfile>(Option<EnrichedUserProfile>.Some(new EnrichedUserProfile(profile, user, badge, extra)))
                        )
                    )
                );
            },
            None: () =>
            {
                logger.LogWarning("No user found.");
                return Task.FromResult<Option<EnrichedUserProfile>>(Option<EnrichedUserProfile>.None);
            }
        );

        enrichedResult.Match(
            Some: profile =>
            {
                logger.LogInformation("Successfully enriched user profile: {@Profile}", profile);
                return Unit.Default;
            },
            None: () =>
            {
                logger.LogWarning("Missing optional data.");
                return Unit.Default;
            }
        );
    }
}

internal record EnrichedUserProfile(
    string Profile,
    string User,
    string Badge,
    string Extra
);

internal static class MockDb
{
    public static IO<Result<Option<string>>> GetUser() =>
        new(() => new Result<Option<string>>(Option<string>.Some("Alice")));

    public static IO<Result<Option<string>>> GetUserProfile(string user) =>
        new(() => new Result<Option<string>>(Option<string>.Some($"Profile of {user}")));

    public static IO<Result<Option<string>>> GetUserProfileBadge(string user) =>
        new(() => new Result<Option<string>>(Option<string>.Some($"Badge for {user}")));

    public static IO<Result<Option<string>>> GetMoreUserStuff(string user) =>
        new(() => new Result<Option<string>>(Option<string>.Some($"Extra info for {user}")));
}

// Example tracer source (replace with your actual tracer)
internal static class MyTracer
{
    public static readonly ActivitySource Source = new("MyApp.Tracer");
}