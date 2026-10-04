using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Planora.Web.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [HttpGet]
    public IActionResult HandleStatusCode(int code)
    {
        Response.StatusCode = code;

        if (code == 404)
        {
            return View("NotFound");
        }

        ViewData["StatusCode"] = code;

        return View("StatusCode");
    }
}

