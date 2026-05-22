using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.Empresas
{
    public class SelectModel : PageModel
    {
        private readonly AtsDbContext _db;

        public SelectModel(AtsDbContext db)
        {
            _db = db;
        }

        public List<Empresa> Empresas { get; set; } = new();
        public string EmpresaActual { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = "/Dashboard";

        public async Task<IActionResult> OnGetAsync()
        {
            EmpresaActual = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";

            Empresas = await _db.Empresas
                .Where(e => e.Activa)
                .OrderBy(e => e.RazonSocial)
                .ToListAsync();

            return Page();
        }
    }
}
