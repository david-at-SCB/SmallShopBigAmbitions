using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;

namespace SmallShopBigAmbitions.Application.HelloWorld;

public class HelloWorldPolicy: IAuthorizationPolicy<HelloWorldRequest> 
{
    public Fin<Unit> Authorize(HelloWorldRequest request, TrustedContext context) =>
        context.IsAuthenticated
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New("Unauthorized: Only authenticated callers can say hello"));
}
