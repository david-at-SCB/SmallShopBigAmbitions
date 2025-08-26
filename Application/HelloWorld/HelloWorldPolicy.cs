using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;

namespace SmallShopBigAmbitions.Application.HelloWorld;

public class HelloWorldPolicy: IAuthorizationPolicy<HelloWorldRequest> 
{
    public Fin<Unit> Authorize(HelloWorldRequest request, TrustedContext context) =>
        context.IsTrusted
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New("Unauthorized: Only trusted callers can say hello"));
}
