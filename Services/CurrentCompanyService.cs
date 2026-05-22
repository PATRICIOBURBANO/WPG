using Microsoft.AspNetCore.Http;
using AtsManager.Models;
using AtsManager;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AtsManager.Services
{
    public interface ICurrentCompanyService
    {
        string Ruc { get; }
        string Nombre { get; }
        bool HasCompany { get; }
        Task LoadAsync();
    }

    public class CurrentCompanyService : ICurrentCompanyService
    {
        private readonly IHttpContextAccessor _http;
        private string _ruc = string.Empty;
        private string _nombre = string.Empty;
        public string Ruc => _ruc;
        public string Nombre => _nombre;
        public bool HasCompany => !string.IsNullOrWhiteSpace(_ruc);
        public CurrentCompanyService(IHttpContextAccessor http)
        {
            _http = http;
        }

        public Task LoadAsync()
        {
            var ruc = _http?.HttpContext?.Session.GetString("EmpresaSeleccionada") ?? string.Empty;
            _ruc = string.IsNullOrWhiteSpace(ruc) ? string.Empty : ruc;
            // Do not access DbContext here to keep this service decoupled; rely on session-stored values
            _nombre = _http?.HttpContext?.Session.GetString("EmpresaNombre") ?? string.Empty;
            return Task.CompletedTask;
        }
    }
}
