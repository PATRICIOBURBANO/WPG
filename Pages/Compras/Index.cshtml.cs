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

        // Lista que se mostrar� en la vista
        public IList<Compra> Compra { get; set; } = default!;
        
        public int TotalRegistros { get; set; }

        // Mensaje para mostrar �xito o error despu�s de una operaci�n
        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        // Recupera todas las compras al cargar la p�gina
        public async Task OnGetAsync()
        {
            ViewData["Title"] = "Listado de Compras (ATS)";
            TotalRegistros = await _db.Compras.CountAsync();
            Compra = await _db.Compras
                .OrderByDescending(c => c.Anio)
                .ThenByDescending(c => c.Mes)
                .ThenByDescending(c => c.FechaEmision)
                .ToListAsync();
        }

        // Maneja la solicitud de eliminaci�n (DELETE)
        public async Task<IActionResult> OnPostDeleteAsync(int? id)
        {
            if (id == null)
            {
                MensajeProceso = "ERROR: No se especific� un ID para eliminar.";
                return RedirectToPage();
            }

            var compra = await _db.Compras.FindAsync(id);

            if (compra != null)
            {
                // No se permite eliminar registros que vinieron por carga por lotes.
                if (compra.CargaLoteId.HasValue)
                {
                    MensajeProceso = "ERROR: No puede eliminar un registro de lote desde esta vista. Use la gesti�n de lotes.";
                    return RedirectToPage();
                }

                _db.Compras.Remove(compra);
                await _db.SaveChangesAsync();
                MensajeProceso = $"�XITO: La compra {compra.NumComprobante} ha sido eliminada correctamente.";
            }
            else
            {
                MensajeProceso = "ERROR: El registro de compra no fue encontrado.";
            }

            return RedirectToPage();
        }
    }
}