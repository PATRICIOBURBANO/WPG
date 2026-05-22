using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AtsManager.Pages.Empresas.Models;

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

        public async Task<IActionResult> OnPostImportarFromPythonAsync()
        {
            var configPath = @"C:\Users\patri\Downloads\SRI\config\config.yaml";
            if (!System.IO.File.Exists(configPath))
            {
                Mensaje = "ERROR: No se encontró config.yaml";
                await OnGetAsync();
                return Page();
            }

            var lines = System.IO.File.ReadAllLines(configPath);
            int importadas = 0, actualizadas = 0;
            object? current = null;
            string? currentRuc = null, currentEmpresa = null, currentClave = null;
            bool inRucs = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("rucs:"))
                {
                    inRucs = true;
                    continue;
                }
                if (!inRucs) continue;

                if (trimmed.StartsWith("- ruc:"))
                {
                    if (currentRuc != null)
                    {
                        var existente = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == currentRuc);
                        if (existente != null)
                        {
                            if (!string.IsNullOrEmpty(currentClave)) existente.ClaveSRI = currentClave;
                            if (!string.IsNullOrEmpty(currentEmpresa)) existente.RazonSocial = currentEmpresa;
                            actualizadas++;
                        }
                        else
                        {
                            _db.Empresas.Add(new Empresa { Ruc = currentRuc, RazonSocial = currentEmpresa ?? "Sin nombre", ClaveSRI = currentClave, Activa = true });
                            importadas++;
                        }
                    }
                    var p = trimmed.IndexOf("'");
                    currentRuc = trimmed.Substring(p + 1, trimmed.LastIndexOf("'") - p - 1).Trim();
                    currentEmpresa = null;
                    currentClave = null;
                    continue;
                }
                if (currentRuc != null)
                {
                    if (trimmed.StartsWith("empresa:"))
                    {
                        var val = trimmed.Substring(trimmed.IndexOf(":") + 1).Trim().Trim('\'');
                        currentEmpresa = val;
                    }
                    else if (trimmed.StartsWith("clave:"))
                    {
                        var val = trimmed.Substring(trimmed.IndexOf(":") + 1).Trim().Trim('\'');
                        currentClave = val;
                    }
                }
            }
            if (currentRuc != null)
            {
                var existente = await _db.Empresas.FirstOrDefaultAsync(e => e.Ruc == currentRuc);
                if (existente != null)
                {
                    if (!string.IsNullOrEmpty(currentClave)) existente.ClaveSRI = currentClave;
                    if (!string.IsNullOrEmpty(currentEmpresa)) existente.RazonSocial = currentEmpresa;
                    actualizadas++;
                }
                else
                {
                    _db.Empresas.Add(new Empresa { Ruc = currentRuc, RazonSocial = currentEmpresa ?? "Sin nombre", ClaveSRI = currentClave, Activa = true });
                    importadas++;
                }
            }

            await _db.SaveChangesAsync();
            Mensaje = $"Importadas: {importadas} nuevas, {actualizadas} actualizadas con clave SRI.";
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
                var claveInput = NuevaEmpresa.ClaveSRI ?? "";
                if (!string.IsNullOrEmpty(claveInput) && claveInput != "••••••••")
                {
                    empresa.ClaveSRI = claveInput;
                }
                await _db.SaveChangesAsync();
                Mensaje = "Empresa actualizada exitosamente.";
            }
            NuevaEmpresa = new Empresa();
            await OnGetAsync();
            return Page();
        }
    }
}
