using AtsManager.Pages.Empresas.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AtsManager.Pages.Empresas
{
    public class SelectorModel : PageModel
    {
        private readonly AtsDbContext _db;
        private const string CookieName = "EmpresaSeleccionada";
        private const int CookieDays = 365;

        public SelectorModel(AtsDbContext db)
        {
            _db = db;
        }

        public List<Empresa> Empresas { get; set; } = new List<Empresa>();
        public string EmpresaActualRuc { get; private set; } = "";
        public string EmpresaActualNombre { get; private set; } = "";

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = "/";

        public async Task<IActionResult> OnGetAsync()
        {
            var rucEmpresa = HttpContext.Session.GetString("EmpresaSeleccionada");
            
            if (string.IsNullOrEmpty(rucEmpresa))
            {
                var rucCookie = Request.Cookies[CookieName];
                if (!string.IsNullOrEmpty(rucCookie))
                {
                    var empresaExiste = await _db.Empresas.AnyAsync(e => e.Ruc == rucCookie && e.Activa);
                    if (empresaExiste)
                    {
                        HttpContext.Session.SetString("EmpresaSeleccionada", rucCookie);
                        var empresa = await _db.Empresas.FirstAsync(e => e.Ruc == rucCookie);
                        HttpContext.Session.SetString("EmpresaNombre", empresa.RazonSocial);
                        rucEmpresa = rucCookie;
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(rucEmpresa))
            {
                var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == rucEmpresa);
                if (empresa != null)
                {
                    EmpresaActualRuc = empresa.Ruc;
                    EmpresaActualNombre = empresa.RazonSocial;
                }
            }
            
            Empresas = await _db.Empresas
                .OrderByDescending(e => e.Activa)
                .ThenBy(e => e.RazonSocial)
                .ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSelectCompany(string ruc, string returnUrl)
        {
            if (string.IsNullOrEmpty(ruc))
            {
                return RedirectToPage();
            }

            var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == ruc);
            if (empresa == null)
            {
                return RedirectToPage();
            }

            HttpContext.Session.SetString("EmpresaSeleccionada", ruc);
            HttpContext.Session.SetString("EmpresaNombre", empresa.RazonSocial);

            Response.Cookies.Append(CookieName, ruc, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(CookieDays),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect("/Dashboard");
        }
    }
}
