using Microsoft.AspNetCore.Mvc;

namespace SmallShopBigAmbitions.Business.Controllers
{
    public class WebHookController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
