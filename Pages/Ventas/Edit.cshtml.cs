using AtsManager.Pages.Empresas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AtsManager.Pages.Ventas
{
    public class EditModel : PageModel
    {
        private readonly AtsDbContext _db;

        public EditModel(AtsDbContext context)
        {
            _db = context;
        }

        [BindProperty]
        public Venta Venta { get; set; } = default!;

        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;

        // Cargar el registro al iniciar la página
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Venta = await _db.Ventas.FirstOrDefaultAsync(m => m.Id == id);

            if (Venta == null)
            {
                MensajeProceso = "Error: El registro de Venta no fue encontrado.";
                return RedirectToPage("./Index");
            }
            return Page();
        }

        // Guardar los cambios al enviar el formulario
        public async Task<IActionResult> OnPostAsync()
        {
            // Validaciones básicas que puede personalizar
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Attach y marcar como modificado para asegurar que EF Core actualice la entidad
                _db.Attach(Venta).State = EntityState.Modified;
                await _db.SaveChangesAsync();
                MensajeProceso = $"✅ ÉXITO: La venta {Venta.NumComprobante} fue actualizada correctamente.";
                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await VentaExists(Venta.Id))
                {
                    return NotFound();
                }
                else
                {
                    // Manejo de concurrencia: si otro usuario editó al mismo tiempo.
                    MensajeProceso = "Error de concurrencia: La venta fue modificada por otro usuario o fue eliminada.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                MensajeProceso = $"❌ Error al guardar la venta: {ex.Message}";
                return Page();
            }
        }

        private async Task<bool> VentaExists(int id)
        {
            return await _db.Ventas.AnyAsync(e => e.Id == id);
        }
    }
}