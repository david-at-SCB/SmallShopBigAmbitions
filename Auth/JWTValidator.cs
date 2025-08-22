namespace SmallShopBigAmbitions.Auth;

public interface IJwtValidator
{
    Fin<TrustedContext> Validate(string token);
}
