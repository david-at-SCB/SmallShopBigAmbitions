//using FunctionalTracing;
//using OpenTelemetry;
//using OpenTelemetry.Resources;
//using OpenTelemetry.Trace;
//using System.Diagnostics;
//using SmallShopBigAmbitions.Logic_examples.FunctionalTracing;

//namespace FunctionalTracing;
//// IMonad<T> interface
//public interface IMonad<T>
//{
//    IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> binder);

//    IMonad<TResult> Map<TResult>(Func<T, TResult> mapper);

//    IMonad<T> Filter(Func<T, bool> predicate);

//    IMonad<TResult> Select<TResult>(Func<T, TResult> selector);

//    IMonad<TSelect> SelectMany<TIntermediate, TSelect>(
//        Func<T, IMonad<TIntermediate>> binder,
//        Func<T, TIntermediate, TSelect> projector);
//}


//public static class Randomizer
//{
//    private static readonly Random _random = new Random();

//    public static bool IsEven() => _random.Next(1, 11) % 2 == 0;
//}

//public class UserService
//{
//    public IO<Result<string>> GetUser() => new IO<Result<string>>(() =>
//        Randomizer.IsEven()
//            ? Result<string>.Ok("user-123")
//            : Result<string>.Fail("User not found")
//    );
//}

//public class DatabaseService
//{
//    public IO<Result<Option<string>>> GetUserProfile(string userId) => new IO<Result<Option<string>>>(() =>
//        Randomizer.IsEven()
//            ? Result<Option<string>>.Ok(Option.Some("Profile for " + userId))
//            : Result<Option<string>>.Ok(Option.None<string>())
//    );

//    public IO<Result<Option<string>>> GetUserBadge(string userId) => new IO<Result<Option<string>>>(() =>
//        Randomizer.IsEven()
//            ? Result<FunctionalTracing.Option.Option<string>>.Ok(Option.Some("Badge for " + userId))
//            : Result<Option<string>>.Fail("DB error while fetching badge")
//    );
//}

//public class NetworkService
//{
//    public IO<Result<string>> FetchExternalData(string userId) => new IO<Result<string>>(() =>
//        Randomizer.IsEven()
//            ? Result<string>.Ok("External data for " + userId)
//            : Result<string>.Fail("Network error")
//    );
//}

//public class Logger
//{
//    public IO<Unit> Log(string message) => new IO<Unit>(() =>
//    {
//        Console.WriteLine("[LOG] " + message);
//        return Unit.Value;
//    });
//}



//public record UserAggregate(string Profile, string Badge, string ExternalData);

//// Example usage
//internal class Tracable
//{
//    // same as before but wrapped in a IO monad
//    public IO<Result<Option<string>>> GetUserProfileBadgeIO() =>
//        new(() => GetUserProfileBadge());

//    private static void Main()
//    {
//        // Result for an optional value, meaning double the monads! :D
//        var result = db.GetUser()
//            .Bind(user => db.GetUserProfile())
//            .TapAndTrace("user.profile.fetch", user => new[] {
//                new KeyValuePair<string, object>("user.id", user.Id),
//                new KeyValuePair<string, object>("user.name", user.Name)
//            })
//            .Bind(profile => db.GetUserProfileBadge())
//            .TapAndTrace("user.badge.fetch", profile => new[] {
//                new KeyValuePair<string, object>("profile.id", profile.Id),
//                new KeyValuePair<string, object>("profile.name", profile.Name)
//            })
//            .Bind(badgeOpt => badgeOpt).TapAndTrace("user.badge.optional", badge => new[] {
//                new KeyValuePair<string, object>("badge.id", badge.Id),
//                new KeyValuePair<string, object>("badge.name", badge.Name)
//            })
//            .Match(
//                some => db.GetMoreUserStuff().Match(
//                    someStuff => Result<string>.Ok("we got it all!"),
//                    () => Result<string>.Fail("no extra user stuff")
//                ),
//                () => Result<string>.Fail("no badge")
//            ));

//        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
//            .AddSource("MyApp.Tracing")
//            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyApp"))
//            .AddConsoleExporter()
//            .Build();

//        // Generic Option
//        IMonad<int> maybe = Option<int>.Some(42)
//            .Map(x => x + 1)
//            .Filter(x => x % 2 == 0)
//            .TapAndTrace("option.trace", x => new[] {
//                    new KeyValuePair<string, object>("value", x)
//            });

//        // Generic Result
//        IMonad<string> result = Result<string>.Ok("Hello")
//            .Map(x => x + " World")
//            .TapAndTrace("result.trace", x => new[] {
//                    new KeyValuePair<string, object>("message", x)
//            });

//        // 1. Option<T> + TapAndTrace
//        var maybeUser = Option.Some(new { Id = 1, Name = "Alice" })
//            .TapAndTrace("user.lookup", user => new[]
//            {
//                    new KeyValuePair<string, object>("user.id", user.Id),
//                    new KeyValuePair<string, object>("user.name", user.Name)
//            });

//        // 2. TracedOption<T>
//        var traced = new TracedOption<int>(Option.Some(42), "initial")
//            .Bind(x => new TracedOption<string>(Option.Some($"Value is {x}"), "format"));

//        Console.WriteLine(traced.Unwrap().Match(
//            Some: val => $"TracedOption result: {val}",
//            None: () => "TracedOption: None"));

//        // 3. Generic Traced<T>
//        var generic = new Traced<int>(10, "start")
//            .Bind(x => new Traced<string>($"Number: {x}", "format"));

//        Console.WriteLine(((Traced<string>)generic).Value);

//        // run a pipeline to get a user, their profile, eventual badges or other info
//        var result = pipeline.Run();
//        Console.WriteLine(result.Match(
//            success => "✅ " + success,
//            error => "❌ " + error
//        ));
//    }

//    ///<summary> Monad pyramid of doom :I ///</summary>
//    public void FunctionalPipeline()
//    {
//        var userService = new UserService();
//        var db = new DatabaseService();
//        var net = new NetworkService();
//        var logger = new Logger();

//        var pipeline = userService.GetUser()
//            .Bind(userResult => logger.Log("Fetched user").Map(_ => userResult))
//            .Bind(userResult => userResult.Match(
//                userId => db.GetUserProfile(userId)
//                    .Bind(profileResult => profileResult.Match(
//                        profile => db.GetUserBadge(userId)
//                            .Bind(badgeResult => badgeResult.Match(
//                                badge => net.FetchExternalData(userId)
//                                    .Bind(netResult => netResult.Match(
//                                        data => logger.Log("Pipeline succeeded").Map(_ => Result<string>.Ok("All data fetched")),
//                                        err => logger.Log(err).Map(_ => Result<string>.Fail(err))
//                                    )),
//                                () => logger.Log("No badge").Map(_ => Result<string>.Fail("No badge"))
//                            )),
//                        () => logger.Log("No profile").Map(_ => Result<string>.Fail("No profile"))
//                    )),
//                err => logger.Log(err).Map(_ => Result<string>.Fail(err))
//            ));
//    }

//    public void FunctionalPipeline_linq()
//    {
//        var userService = new UserService();
//        var db = new DatabaseService();
//        var net = new NetworkService();
//        var logger = new Logger();

//        from userResult in userService.GetUser()
//        from _1 in logger.Log($"User fetch result: {userResult}")
//        from profileResult in userResult.Match(
//            userId => db.GetUserProfile(userId),
//            err => IO<Result<Option<string>>>.Pure(Result<Option<string>>.Fail(err))
//        )
//        from _2 in logger.Log($"Profile fetch result: {profileResult}")
//        from badgeResult in profileResult.Match(
//            profile => db.GetUserBadge("user-123"),
//            () => IO<Result<Option<string>>>.Pure(Result<Option<string>>.Fail("No profile"))
//        )
//        from _3 in logger.Log($"Badge fetch result: {badgeResult}")
//        from netResult in badgeResult.Match(
//            badge => net.FetchExternalData("user-123"),
//            () => IO<Result<string>>.Pure(Result<string>.Fail("No badge"))
//        )
//        from _4 in logger.Log($"Network fetch result: {netResult}")
//        select netResult.Match(
//            data => Result<UserAggregate>.Ok(new UserAggregate(
//                profileResult.Match(p => p, () => "N/A"),
//                badgeResult.Match(b => b, () => "N/A"),
//                data
//            )),
//            err => Result<UserAggregate>.Fail(err)
//        );

//        //var pipeline =
//        //    from userResult in userService.GetUser()
//        //    from _1 in logger.Log($"User fetch result: {userResult}")
//        //    from profileResult in userResult.Match(
//        //        userId => db.GetUserProfile(userId),
//        //        err => IO<Result<Option<string>>>.Pure(Result<Option<string>>.Fail(err)))

//        //    from _2 in logger.Log($"Profile fetch result: {profileResult}")
//        //    from badgeResult in profileResult.Match(
//        //        profile => db.GetUserBadge("user-123"),
//        //        () => IO<Result<Option<string>>>.Pure(Result<Option<string>>.Fail("No profile")))
//        //    from _3 in logger.Log($"Badge fetch result: {badgeResult}")
//        //    from netResult in badgeResult.Match(
//        //        badge => net.FetchExternalData("user-123"),
//        //        () => IO<Result<string>>.Pure(Result<string>.Fail("No badge")))
//        //    from _4 in logger.Log($"Network fetch result: {netResult}")
//        //    select netResult;
//    }
//}