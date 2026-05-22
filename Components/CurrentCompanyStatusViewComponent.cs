using Microsoft.AspNetCore.Mvc;
using AtsManager.Services;

namespace AtsManager.Components
{
    public class CurrentCompanyStatusViewComponent : ViewComponent
    {
        private readonly ICurrentCompanyService _currentCompany;
        public CurrentCompanyStatusViewComponent(ICurrentCompanyService currentCompany)
        {
            _currentCompany = currentCompany;
        }
        public IViewComponentResult Invoke()
        {
            var model = new {
                HasCompany = _currentCompany.HasCompany,
                Nombre = _currentCompany.Nombre,
                Ruc = _currentCompany.Ruc
            };
            return View(model);
        }
    }
}
