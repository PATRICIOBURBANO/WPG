using Microsoft.AspNetCore.Mvc; // Necesario para [TempData] y [BindProperty]
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AtsManager.Pages.Retenciones
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;

        public IndexModel(AtsDbContext context)
        {
            _db = context;
        }
        [TempData]
        public string MensajeProceso { get; set; } = string.Empty; // ¡AÑADIR ESTO!
        // Propiedad para almacenar el listado de retenciones
        public IList<RetencionCliente> Retenciones { get; set; } = new List<RetencionCliente>();

        public async Task OnGetAsync()
        {
            // Cargar todas las retenciones, ordenadas por fecha de retención
            Retenciones = await _db.RetencionesClientes
                .OrderByDescending(r => r.FechaRetencion)
                .ToListAsync();
        }
    }
}