using LanguageExt;

namespace SmallShopBigAmbitions.Models;

public readonly record struct ExternalProductRef(int ApiProductId);

public readonly record struct Quantity
{
    public int Value { get; }
    private Quantity(int value) => Value = value;

    public static Fin<Quantity> Create(int value) =>
        value > 0
            ? FinSucc(new Quantity(value))
            : FinFail<Quantity>(Error.New("Quantity must be > 0"));

    public static implicit operator int(Quantity q) => q.Value;
}