using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public record ChargeCustomerCommand(Guid CartId, Guid UserId) 
    : IFunctionalRequest<ChargeResult>;