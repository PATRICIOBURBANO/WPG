using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Models;
using System;
using System.Threading.Tasks;

namespace AtsManager.Pages.NCCompras
{
    public class CreateModel : PageModel
    {
        private readonly AtsDbContext _db;

        [BindProperty]
        public NCCompra Registro { get; set; } = new NCCompra();

        public CreateModel(AtsDbContext context)
        {
            _db = context;
        }

        public void OnGet()
        {
            Registro.FechaEmision = DateTime.Now.Date;
            Registro.Anio = DateTime.Now.Year;
            Registro.Mes = DateTime.Now.Month;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            Registro.FechaRegistro = DateTime.Now;
            Registro.UsuarioCreacion = User?.Identity?.Name ?? "Ingreso Manual";
            Registro.FechaCreacion = DateTime.Now;

            _db.NCCompras.Add(Registro);
            await _db.SaveChangesAsync();

            TempData["MensajeExito"] = "Nota de crédito guardada correctamente.";
            return RedirectToPage("./Index");
        }
    }
}
