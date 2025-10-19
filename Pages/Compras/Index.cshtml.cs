using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Models;

namespace AtsManager.Pages.Compras
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;

        public IndexModel(AtsDbContext context)
        {
            _db = context;
        }

        // Lista que se mostrará en la vista
        public IList<Compra> Compra { get; set; } = default!;

        // Mensaje para mostrar éxito o error después de una operación
        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        // Recupera todas las compras al cargar la página
        public async Task OnGetAsync()
        {
            ViewData["Title"] = "Listado de Compras (ATS)";
            Compra = await _db.Compras
                .OrderByDescending(c => c.Anio)
                .ThenByDescending(c => c.Mes)
                .ThenByDescending(c => c.FechaEmision)
                .ToListAsync();
        }

        // Maneja la solicitud de eliminación (DELETE)
        public async Task<IActionResult> OnPostDeleteAsync(int? id)
        {
            if (id == null)
            {
                MensajeProceso = "ERROR: No se especificó un ID para eliminar.";
                return RedirectToPage();
            }

            var compra = await _db.Compras.FindAsync(id);

            if (compra != null)
            {
                // No se permite eliminar registros que vinieron por carga por lotes.
                if (compra.CargaLoteId.HasValue)
                {
                    MensajeProceso = "ERROR: No puede eliminar un registro de lote desde esta vista. Use la gestión de lotes.";
                    return RedirectToPage();
                }

                _db.Compras.Remove(compra);
                await _db.SaveChangesAsync();
                MensajeProceso = $"ÉXITO: La compra {compra.NumComprobante} ha sido eliminada correctamente.";
            }
            else
            {
                MensajeProceso = "ERROR: El registro de compra no fue encontrado.";
            }

            return RedirectToPage();
        }
    }
}