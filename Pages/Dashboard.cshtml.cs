using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Services;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages
{
    public class DashboardModel : AtsManager.Pages.ReportBasePageModel
    {
        private readonly AtsDbContext _db;

        public DashboardModel(AtsDbContext context, ICurrentCompanyService currentCompany) : base(currentCompany) 
        {
            _db = context;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadCurrentCompanyAsync();
            return Page();
        }
    }
}
