using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Models;

namespace AtsManager.Pages.Ventas
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;

        public IndexModel(AtsDbContext context)
        {
            _db = context;
        }

        // Lista que se mostrará en la vista
        public IList<Venta> Venta { get; set; } = default!;

        // Mensaje para mostrar éxito o error después de una operación
        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        // Recupera todas las ventas al cargar la página
        public async Task OnGetAsync()
        {
            ViewData["Title"] = "Listado de Ventas (ATS)";
            Venta = await _db.Ventas
                .OrderByDescending(v => v.Anio)
                .ThenByDescending(v => v.Mes)
                .ThenByDescending(v => v.FechaEmision)
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

            var venta = await _db.Ventas.FindAsync(id);

            if (venta != null)
            {
                // No se permite eliminar registros que vinieron por carga por lotes.
                if (venta.CargaLoteId.HasValue)
                {
                    MensajeProceso = "ERROR: No puede eliminar un registro de lote desde esta vista. Use la gestión de lotes.";
                    return RedirectToPage();
                }

                _db.Ventas.Remove(venta);
                await _db.SaveChangesAsync();
                MensajeProceso = $"ÉXITO: La venta {venta.NumComprobante} ha sido eliminada correctamente.";
            }
            else
            {
                MensajeProceso = "ERROR: El registro de venta no fue encontrado.";
            }

            return RedirectToPage();
        }
    }
}