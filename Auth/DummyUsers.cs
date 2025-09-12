namespace SmallShopBigAmbitions.Auth;

public record DummyUser(Guid Id, string Email, string DisplayName, string[] Roles);

public interface IDummyUserStore
{
    IReadOnlyList<DummyUser> All { get; }
    DummyUser? Get(Guid id);
}

public sealed class InMemoryDummyUserStore : IDummyUserStore
{
    private static readonly List<DummyUser> _users = new()
    {
        new DummyUser(Guid.Parse("11111111-1111-1111-1111-111111111111"), "alice@example.test", "Alice", new[]{"User"}),
        new DummyUser(Guid.Parse("22222222-2222-2222-2222-222222222222"), "bob@example.test", "Bob", new[]{"User"}),
        new DummyUser(Guid.Parse("33333333-3333-3333-3333-333333333333"), "admin@example.test", "AdminUser", new[]{"Admin"})
    };

    public IReadOnlyList<DummyUser> All => _users;

    public DummyUser? Get(Guid id) => _users.FirstOrDefault(u => u.Id == id);
}
