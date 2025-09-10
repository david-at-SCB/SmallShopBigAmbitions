namespace SmallShopBigAmbitions.Application._Policy;

public static class ErrorCodes
{
    // Generic/auth
    public const string Auth_Unauthorized = "auth.unauthorized";
    public const string Auth_InsufficientRole = "auth.insufficient_role";

    // Cart / Add Item
    public const string Cart_Add_QuantityNonPositive = "cart.add.quantity_non_positive";
    public const string Cart_Add_QuantityTooLarge = "cart.add.quantity_too_large";
    public const string Cart_Add_CurrencyMissing = "cart.add.currency_missing";
    public const string Cart_Add_TooManyLines = "cart.add.too_many_lines";

    // Cart / Remove Item
    public const string Cart_Remove_NotInCart = "cart.remove.not_in_cart";

    // Cart validation
    public const string Cart_Empty = "cart.validation.empty";
    public const string Cart_InvalidQuantity = "cart.validation.invalid_quantity";
    public const string Cart_InvalidPrice = "cart.validation.invalid_price";
    public const string Cart_TotalInvalid = "cart.validation.total_non_positive";

    // Payment / Intent
    public const string Payment_Intent_MethodRequired = "payment.intent.method_required";
    public const string Payment_Intent_CartEmpty = "payment.intent.cart_empty";

    // Refund
    public const string Payment_Refund_AmountNonPositive = "payment.refund.amount_non_positive";
}
