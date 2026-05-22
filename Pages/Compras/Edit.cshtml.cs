using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.Compras
{
    // Clase para el CRUD de edici�n
    public class EditModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;

        // Propiedad a enlazar: el registro de Compra que se va a editar
        [BindProperty]
        public Compra RegistroCompra { get; set; }

        // Datos para los campos de selecci�n (Tipo ID y Comprobante)
        public List<string> TiposId { get; set; } = new List<string> { "01", "02", "03", "07", "08" };
        public List<string> TiposComprobante { get; set; } = new List<string> { "01", "04", "05", "41" };

        public EditModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
        {
            _db = context;
        }

        // Recupera el registro existente por ID
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            await LoadCurrentCompanyAsync();
            if (id == null)
            {
                return NotFound();
            }

            // Busca el registro en la base de datos
            RegistroCompra = await _db.Compras.FirstOrDefaultAsync(m => m.Id == id);

            if (RegistroCompra == null)
            {
                return NotFound();
            }

            ViewData["Title"] = $"Editar Compra: {RegistroCompra.NumComprobante}";
            return Page();
        }

        // Procesa la actualizaci�n del formulario
        public async Task<IActionResult> OnPostAsync()
        {
            // Remover las validaciones del objeto CargaLote (no aplica a edici�n manual)
            ModelState.Remove("RegistroCompra.CargaLote");

            if (!ModelState.IsValid)
            {
                // Si la validaci�n falla, regresa a la p�gina para mostrar errores
                return Page();
            }

            try
            {
                // Adjunta el registro al contexto y marca el estado como Modificado
                _db.Attach(RegistroCompra).State = EntityState.Modified;

                // 1. Asegurar que los campos de Lote no se modifiquen en una edici�n manual
                _db.Entry(RegistroCompra).Property(x => x.CargaLoteId).IsModified = false;
                _db.Entry(RegistroCompra).Property(x => x.FechaCreacion).IsModified = false;
                _db.Entry(RegistroCompra).Property(x => x.UsuarioCreacion).IsModified = false;

                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Manejo de concurrencia: si el registro ya no existe
                if (!_db.Compras.Any(e => e.Id == RegistroCompra.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // Redirige al listado principal de compras
            return RedirectToPage("./Index");
        }
    }
}
