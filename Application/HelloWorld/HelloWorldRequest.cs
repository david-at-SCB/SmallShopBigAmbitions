using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.HelloWorld;

public record HelloWorldRequest(string Name) 
    : IFunctionalRequest<string>;