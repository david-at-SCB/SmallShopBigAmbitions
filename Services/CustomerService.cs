using LanguageExt.Pipes;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads;
using System.Net.Http.Metrics;


namespace SmallShopBigAmbitions.Services;
public record EnrichedCustomerProfile(string Profile, string Name, string Badge, string Extra);

public interface IMockDatabase
{
    IO<Customer> GetUser();
    IO<string> GetUserProfile(Customer user);
    IO<string> GetUserProfileBadge(Customer user);
    IO<string> GetMoreUserStuff(Customer user);
}

public class CustomerService
{
    public void TODO() => Console.WriteLine("Make this example work");

    //    public EnrichedCustomerProfile GetDetailedCustomerProfile(
    //        IMockDatabase db)
    //    {
    //        // Step 1: Get user
    //        var customerResult = new Traceable<IO<Customer>>(
    //            () => db.GetUser(),
    //            "user.fetch",
    //            customer => new[] { KeyValuePair.Create("user.id", (object)customer.Select(cust => cust.Name)) }
    //        ).Run();
    //        // Step 2: Parallel fetches
    //        var enrichedResult = customerResult.Bind(user =>
    //        {
    //            var profileT = new Traceable<IO<string>>(
    //                () => db.GetUserProfile(user),
    //                "user.profile.fetch",
    //                profile => new[] { KeyValuePair.Create("profile.length", (object)profile.Length) }
    //            );
    //            var badgeT = new Traceable<IO<string>>(
    //                () => db.GetUserProfileBadge(user),
    //                "user.badge.fetch"
    //            );
    //            var extraT = new Traceable<IO<string>>(
    //                () => db.GetMoreUserStuff(user),
    //                "user.extra.fetch"
    //            );
    //            // Run in parallel
    //            var profile = profileT.Run();
    //            var badge = badgeT.Run();
    //            var extra = extraT.Run();
    //            return IO(() => new EnrichedUserProfile(profile.Run(), user.Name, badge.Run(), extra.Run()));
    //        });
    //        return context;
    //    }

    //    public EnrichedCustomerProfile GetDetailedCustomerProfileWithParallelFetches(
    //        IMockDatabase db)
    //    {

    //        // Step 1: Get user
    //        var userResult = new Traceable<IO<User>>(
    //            () => db.GetUser(),
    //            "user.fetch",
    //            user => new[] { KeyValuePair.Create("user.id", (object)user.Id) }
    //        ).Run();

    //        // Step 2: Parallel fetches
    //        var enrichedResult = userResult.Bind(user =>
    //        {
    //            var profileT = new Traceable<IO<string>>(
    //                () => db.GetUserProfile(user),
    //                "user.profile.fetch",
    //                profile => new[] { KeyValuePair.Create("profile.length", (object)profile.Length) }
    //            );

    //            var badgeT = new Traceable<IO<string>>(
    //                () => db.GetUserProfileBadge(user),
    //                "user.badge.fetch"
    //            );

    //            var extraT = new Traceable<IO<string>>(
    //                () => db.GetMoreUserStuff(user),
    //                "user.extra.fetch"
    //            );

    //            // Run in parallel
    //            var profile = profileT.Run();
    //            var badge = badgeT.Run();
    //            var extra = extraT.Run();

    //            return IO(() => new EnrichedUserProfile(profile.Run(), user.Name, badge.Run(), extra.Run()));
    //        });

    //    }

}