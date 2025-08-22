using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.HelloWorld;

public class HelloWorldHandler : IFunctionalHandler<HelloWorldRequest, string>
{
    public IO<Fin<string>> Handle(HelloWorldRequest request, TrustedContext context, CancellationToken ct) =>
      from _ in AuthorizationGuards.RequireTrustedORThrow(context)
      select Fin<string>.Succ($"Hello, {request.Name}! (Caller: {context.CallerId}, Role: {context.Role})");
}
