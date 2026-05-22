using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Models;
using System;
using System.Threading.Tasks;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.NCCompras
{
    public class CreateModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;

        [BindProperty]
        public NCCompra Registro { get; set; } = new NCCompra();

        public CreateModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
        {
            _db = context;
        }

        public async Task OnGetAsync()
        {
            await LoadCurrentCompanyAsync();
            Registro.FechaEmision = DateTime.Now.Date;
            Registro.Anio = DateTime.Now.Year;
            Registro.Mes = DateTime.Now.Month;
            // Pre-fill RUC con la empresa activa si no está definido
            if (string.IsNullOrWhiteSpace(Registro.RucEmisor))
            {
                Registro.RucEmisor = CurrentRuc;
            }
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

            TempData["MensajeExito"] = "Nota de cr�dito guardada correctamente.";
            return RedirectToPage("./Index");
        }
    }
}
