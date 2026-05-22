using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AtsManager.Pages.Empresas.Models;

namespace AtsManager.Pages.Administrador.Cargas
{
    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;
        public IndexModel(AtsDbContext db)
        {
            _db = db;
        }

        public List<CargaLote> Cargas { get; set; } = new List<CargaLote>();

        public async Task OnGetAsync()
        {
            Cargas = await _db.CargasLotes.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync();
        }

        public async Task<IActionResult> OnPostUploadLoteAsync(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                return RedirectToPage();
            }

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploads);
            var ruta = Path.Combine(uploads, Path.GetFileName(archivo.FileName));
            using (var stream = new FileStream(ruta, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            int totalRegistros = 0;
            try
            {
                var content = await System.IO.File.ReadAllTextAsync(ruta);
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                totalRegistros = lines.Length;
            }
            catch { totalRegistros = 0; }

            var lote = new CargaLote
            {
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month,
                TipoArchivo = Path.GetExtension(archivo.FileName).TrimStart('.').ToUpper(),
                NombreArchivo = archivo.FileName,
                TotalRegistros = totalRegistros
            };
            _db.CargasLotes.Add(lote);
            await _db.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteLoteAsync(int id)
        {
            var lote = await _db.CargasLotes.FindAsync(id);
            if (lote != null)
            {
                _db.CargasLotes.Remove(lote);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
