using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Pages.Empresas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AtsManager.Pages.Retenciones.Compras
{
    public class IndexModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;
        public List<RetencionCliente> Retenciones { get; set; } = new List<RetencionCliente>();

        public IndexModel(AtsDbContext context, AtsManager.Services.ICurrentCompanyService currentCompany) : base(currentCompany)
        {
            _db = context;
        }

        public async Task OnGetAsync()
        {
            await LoadCurrentCompanyAsync();
            string ruc = CurrentRuc;
            var query = _db.RetencionesClientes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(ruc))
            {
                query = query.Where(r => r.RucEmpresa == ruc);
            }
            Retenciones = await query.OrderByDescending(r => r.FechaRetencion).ToListAsync();
        }
    }
}
