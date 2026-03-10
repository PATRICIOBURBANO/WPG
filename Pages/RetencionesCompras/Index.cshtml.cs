using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AtsManager.Pages.RetencionesCompras
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;

        public IndexModel(AtsDbContext context)
        {
            _db = context;
        }

        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        public IList<RetencionCompra> Retenciones { get; set; } = new List<RetencionCompra>();

        public async Task OnGetAsync()
        {
            Retenciones = await _db.RetencionesCompras
                .OrderByDescending(r => r.FechaRetencion)
                .ToListAsync();
        }
    }
}
