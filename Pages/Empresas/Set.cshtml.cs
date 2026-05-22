using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.Empresas
{
    public class SetModel : PageModel
    {
        private readonly AtsDbContext _db;
        private const string CookieName = "EmpresaSeleccionada";
        private const int CookieDays = 365;

        public SetModel(AtsDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public string Ruc { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = "/Dashboard";

        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(Ruc))
                return Redirect("/");

            HttpContext.Session.SetString("EmpresaSeleccionada", Ruc);

            var emp = _db.Empresas.FirstOrDefault(e => e.Ruc == Ruc);
            HttpContext.Session.SetString("EmpresaNombre", emp?.RazonSocial ?? Ruc);

            Response.Cookies.Append(CookieName, Ruc, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(CookieDays),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                return Redirect(ReturnUrl);

            return Redirect("/Dashboard");
        }
    }
}
