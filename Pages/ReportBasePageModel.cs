using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;
using AtsManager.Services;

namespace AtsManager.Pages
{
    // Base page model to provide centralized current company context for report-related pages
    public abstract class ReportBasePageModel : PageModel
    {
        protected readonly ICurrentCompanyService _currentCompany;

        protected string CurrentRuc => _currentCompany?.Ruc ?? string.Empty;
        protected string CurrentNombre => _currentCompany?.Nombre ?? string.Empty;
        protected bool HasCompany => _currentCompany != null && _currentCompany.HasCompany;

        protected ReportBasePageModel(ICurrentCompanyService currentCompany)
        {
            _currentCompany = currentCompany;
        }

        public async Task LoadCurrentCompanyAsync()
        {
            if (_currentCompany != null)
            {
                await _currentCompany.LoadAsync();
            }
        }

        // Public aliases for views to access current company info
        public string CurrentRucPublic => CurrentRuc;
        public string CurrentNombrePublic => CurrentNombre;
        public bool HasCompanyPublic => HasCompany;
    }
}
