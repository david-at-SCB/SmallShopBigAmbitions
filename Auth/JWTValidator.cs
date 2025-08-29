using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace SmallShopBigAmbitions.Auth;

public interface IJwtValidator
{
    IO<Fin<TrustedContext>> ValidateAsync(string? token);
}

public class JwtValidator(IConfiguration config) : IJwtValidator
{
    private readonly IConfiguration _config = config;

    // Declarative/functional JWT validation using IO + Fin
    // Wrap ValidateTokenAsync in IO to avoid async leaking
    public IO<Fin<TrustedContext>> ValidateAsync(string? token) =>
        IO.liftAsync(async () =>
        {
            // Pure validation and parameter building in Fin
            var pre =
                from t in RequireNonEmpty(token, "Missing bearer token")
                from key in RequireNonEmpty(_config["Jwt:Key"], "JWT key not configured")
                let issuer = _config["Jwt:Issuer"]
                let audience = _config["Jwt:Audience"]
                let parameters = BuildParams(key, issuer, audience)
                select (t, parameters);

            // Bind into the async validator without try/catch or branching
            var finJwt = await BindAsync(pre, async tp =>
            {
                var handler = new JsonWebTokenHandler();
                var result = await handler.ValidateTokenAsync(tp.t, tp.parameters);
                return ToJwt(result);
            });

            // Map claims to TrustedContext
            return finJwt.Map(jwt =>
            {
                var callerId = ParseGuid(
                    jwt.GetPayloadValue<string>(ClaimTypes.NameIdentifier)
                    ?? jwt.GetPayloadValue<string>("sub"));
                var role = jwt.GetPayloadValue<string>(ClaimTypes.Role) ?? "User";
                return new TrustedContext
                {
                    CallerId = callerId,
                    Role = role
                };
            });
        });

    private static TokenValidationParameters BuildParams(string key, string issuer, string? audience) =>
        new()
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
            ValidIssuer = issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

    private static Fin<string> RequireNonEmpty(string? value, string error) =>
        Optional(value)
            .Filter(v => !string.IsNullOrWhiteSpace(v))
            .Match(
                Some: Fin<string>.Succ,
                None: () => Fin<string>.Fail(Error.New(error))
            );

    private static Fin<JsonWebToken> ToJwt(TokenValidationResult result) =>
        (result.IsValid && result.SecurityToken is JsonWebToken j)
            ? Fin<JsonWebToken>.Succ(j)
            : Fin<JsonWebToken>.Fail(Error.New(result.Exception ?? new SecurityTokenException("Invalid token")));

    private static async Task<Fin<B>> BindAsync<A, B>(Fin<A> fin, Func<A, Task<Fin<B>>> f) =>
        await fin.Match(
            Succ: f,
            Fail: e => Task.FromResult(Fin<B>.Fail(e))
        );

    private static Guid ParseGuid(string? str) =>
        Guid.TryParse(str, out var id) ? id : Guid.Empty;

}