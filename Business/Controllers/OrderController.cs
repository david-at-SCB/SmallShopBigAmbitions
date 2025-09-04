using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SmallShopBigAmbitions.Business.Controllers;

public class OrderController : Controller
{
    // GET: OrderController
    public ActionResult Index()
    {
        return View();
    }

    // POST: OrderController/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Create(IFormCollection collection)
    {
        try
        {
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            return View();
        }
    }

}