using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Pages.Empresas.Models;
using Microsoft.EntityFrameworkCore;

namespace AtsManager.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AtsDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, AtsDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult OnGet()
        {
            var rucEmpresa = HttpContext.Session.GetString("EmpresaSeleccionada");
            
            if (string.IsNullOrEmpty(rucEmpresa))
            {
                var rucCookie = Request.Cookies["EmpresaSeleccionada"];
                if (!string.IsNullOrEmpty(rucCookie))
                {
                    var empresaExiste = _context.Empresas.Any(e => e.Ruc == rucCookie && e.Activa);
                    if (empresaExiste)
                    {
                        HttpContext.Session.SetString("EmpresaSeleccionada", rucCookie);
                        var empresa = _context.Empresas.First(e => e.Ruc == rucCookie);
                        HttpContext.Session.SetString("EmpresaNombre", empresa.RazonSocial);
                    }
                }
            }

            return RedirectToPage("/Dashboard");
        }
    }
}
