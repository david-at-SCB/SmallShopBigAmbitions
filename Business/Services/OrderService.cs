using SmallShopBigAmbitions.Database;

namespace SmallShopBigAmbitions.Business.Services;

// Placeholder service (file previously had syntax errors). Flesh out later as needed.
public interface IOrderService { }

public sealed class OrderService(IDataAccess dataAccess) : IOrderService
{
    private readonly IDataAccess _dataAccess = dataAccess;
}
