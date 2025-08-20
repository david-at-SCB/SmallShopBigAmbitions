using MediatR;
using System.Diagnostics;
using SmallShopBigAmbitions.TracingSources;

namespace SmallShopBigAmbitions.Auth;

public class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        using var activity = Telemetry.MediatorSource.StartActivity($"MediatR {typeof(TRequest).Name}");
        if (activity != null)
        {
            activity.SetTag("mediatr.request.type", typeof(TRequest).FullName ?? typeof(TRequest).Name);
            activity.SetTag("mediatr.response.type", typeof(TResponse).FullName ?? typeof(TResponse).Name);

            if (request is IAuthorizedRequest ar)
            {
                activity.SetTag("auth.caller_id", ar.Context.CallerId);
                activity.SetTag("auth.role", ar.Context.Role ?? string.Empty);
            }
        }

        return await next(ct);
    }
}
