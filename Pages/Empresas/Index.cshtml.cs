using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AtsManager.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;

        public IndexModel(AtsDbContext db)
        {
            _db = db;
        }

        public List<Empresa> Empresas { get; set; } = new List<Empresa>();

        [BindProperty]
        public Empresa NuevaEmpresa { get; set; } = new Empresa();

        public bool EmpresaActiva { get; set; } = true;

        public string Mensaje { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            Empresas = await _db.Empresas.OrderBy(e => e.RazonSocial).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(NuevaEmpresa.Ruc) || string.IsNullOrWhiteSpace(NuevaEmpresa.RazonSocial))
            {
                Mensaje = "ERROR: RUC y Razon Social son obligatorios.";
                await OnGetAsync();
                return Page();
            }

            if (NuevaEmpresa.Ruc.Length != 13)
            {
                Mensaje = "ERROR: El RUC debe tener 13 digitos.";
                await OnGetAsync();
                return Page();
            }

            var existente = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == NuevaEmpresa.Ruc);
            if (existente != null)
            {
                Mensaje = "ERROR: Ya existe una empresa con ese RUC.";
                await OnGetAsync();
                return Page();
            }

            _db.Empresas.Add(NuevaEmpresa);
            await _db.SaveChangesAsync();
            
            Mensaje = "Empresa agregada exitosamente.";
            NuevaEmpresa = new Empresa();
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var empresa = await _db.Empresas.FindAsync(id);
            if (empresa != null)
            {
                _db.Empresas.Remove(empresa);
                await _db.SaveChangesAsync();
                Mensaje = "Empresa eliminada.";
            }
            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEditarAsync(int id)
        {
            var empresa = await _db.Empresas.FindAsync(id);
            if (empresa != null)
            {
                empresa.Ruc = NuevaEmpresa.Ruc;
                empresa.RazonSocial = NuevaEmpresa.RazonSocial;
                empresa.Direccion = NuevaEmpresa.Direccion;
                empresa.CodEstablecimiento = NuevaEmpresa.CodEstablecimiento;
                empresa.Activa = EmpresaActiva;
                await _db.SaveChangesAsync();
                Mensaje = "Empresa actualizada exitosamente.";
            }
            NuevaEmpresa = new Empresa();
            await OnGetAsync();
            return Page();
        }
    }
}
