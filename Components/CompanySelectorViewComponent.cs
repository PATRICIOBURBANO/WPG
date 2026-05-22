using Microsoft.AspNetCore.Http;
using AtsManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Components
{
    [ViewComponent(Name = "CompanySelector")]
    public class CompanySelectorViewComponent : ViewComponent
    {
        private readonly AtsDbContext _db;
        private readonly ICurrentCompanyService _currentCompany;

        public CompanySelectorViewComponent(AtsDbContext db, ICurrentCompanyService currentCompany)
        {
            _db = db;
            _currentCompany = currentCompany;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Load current company from the centralized service
            await _currentCompany.LoadAsync();
            var ruc = _currentCompany.Ruc;
            var nombre = _currentCompany.Nombre;
            
            var model = new CompanySelectorViewModel
            {
                Ruc = ruc,
                Nombre = nombre,
                HasCompanySelected = !string.IsNullOrEmpty(ruc)
            };
            
            var todasEmpresas = await _db.Empresas
                .Where(e => e.Activa)
                .OrderBy(e => e.RazonSocial)
                .ToListAsync();
            ViewBag.OtrasEmpresas = todasEmpresas;
            
            return View(model);
        }
    }

    public class CompanySelectorViewModel
    {
        public string Ruc { get; set; } = "";
        public string Nombre { get; set; } = "";
        public bool HasCompanySelected { get; set; }
    }
}
