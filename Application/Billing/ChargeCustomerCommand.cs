using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Billing;

public record ChargeCustomerCommand(Guid CartId, Guid UserId, TrustedContext Context)
    : IRequest<Fin<ChargeResult>>;