using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AtsManager.Services;
using AtsManager.Pages.Empresas.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
 
namespace AtsManager.Pages.Descargas
{
    public class DescargaProgramada
    {
        public int Id { get; set; }
        public string Empresa { get; set; } = "";
        public DateTime FechaProgramada { get; set; }
        public string Estado { get; set; } = "Pendiente";
    }

    public class IndexModel : PageModel
    {
        private readonly AtsDbContext _db;

        public IndexModel(AtsDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public int Anio { get; set; } = DateTime.Now.Year;

        [BindProperty]
        public int Mes { get; set; } = DateTime.Now.Month;

        [BindProperty]
        public bool DescargarRecibidos { get; set; } = true;

        [BindProperty]
        public bool DescargarEmitidos { get; set; } = true;

        [BindProperty]
        public bool Facturas { get; set; } = true;

        [BindProperty]
        public bool NotasCredito { get; set; } = true;

        [BindProperty]
        public bool NotasDebito { get; set; } = true;

        [BindProperty]
        public bool Retenciones { get; set; } = true;

        [BindProperty]
        public DateTime? FechaDesde { get; set; }

        [BindProperty]
        public DateTime? FechaHasta { get; set; }

        [BindProperty]
        public DateTime? FechaProgramada { get; set; }

        public List<EmpresaSRI> EmpresasSRI { get; set; } = new();

        public List<DescargaProgramada> DescargasProgramadas { get; set; } = new();

        public string EmpresaSeleccionadaRuc { get; set; } = "";

        public string Mensaje { get; set; } = "";

        public bool Ejecutando { get; set; } = false;

        public List<string> Resultado { get; set; } = new();

        public void OnGet()
        {
            EmpresaSeleccionadaRuc = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";
            CargarEmpresas();
            CargarDescargasProgramadas();
        }

        private void CargarEmpresas()
        {
            EmpresasSRI = new List<EmpresaSRI>();

            var empresasDb = _db.Empresas.Where(e => !string.IsNullOrEmpty(e.ClaveSRI)).ToList();

            foreach (var emp in empresasDb)
            {
                EmpresasSRI.Add(new EmpresaSRI
                {
                    ruc = emp.Ruc?.Trim().Trim('"', ' ', '\'') ?? "",
                    empresa = emp.RazonSocial?.Trim() ?? "",
                    clave = emp.ClaveSRI ?? ""
                });
            }

            if (EmpresasSRI.Count == 0)
            {
                try
                {
                    string configPath = @"C:\Users\patri\Downloads\SRI\config\config.yaml";
                    if (System.IO.File.Exists(configPath))
                    {
                        var yaml = System.IO.File.ReadAllText(configPath);
                        var lineas = yaml.Split('\n');
                        
                        string rucActual = "", empresaActual = "", claveActual = "";
                        bool enBloque = false;

                        foreach (var linea in lineas)
                        {
                            var trimmed = linea.Trim();
                            
                            if (trimmed == "- ruc:" || trimmed.StartsWith("- ruc:"))
                            {
                                if (!string.IsNullOrEmpty(rucActual) && !string.IsNullOrEmpty(empresaActual))
                                {
                                    EmpresasSRI.Add(new EmpresaSRI
                                    {
                                        ruc = rucActual,
                                        empresa = empresaActual,
                                        clave = claveActual
                                    });
                                }
                                rucActual = "";
                                empresaActual = "";
                                claveActual = "";
                                enBloque = true;
                                
                                var parts = trimmed.Split(':');
                                if (parts.Length > 1)
                                    rucActual = parts[1].Trim().Trim('"');
                            }
                            else if (enBloque && trimmed.StartsWith("empresa:"))
                            {
                                var parts = trimmed.Split(':');
                                if (parts.Length > 1)
                                    empresaActual = parts[1].Trim().Trim('"');
                            }
                            else if (enBloque && trimmed.StartsWith("clave:"))
                            {
                                var parts = trimmed.Split(':');
                                if (parts.Length > 1)
                                    claveActual = parts[1].Trim().Trim('"');
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(rucActual) && !string.IsNullOrEmpty(empresaActual))
                        {
                            EmpresasSRI.Add(new EmpresaSRI
                            {
                                ruc = rucActual,
                                empresa = empresaActual,
                                clave = claveActual
                            });
                        }
                    }
                }
                catch (Exception ex) 
                { 
                }
            }
        }

        private void CargarDescargasProgramadas()
        {
            DescargasProgramadas = new List<DescargaProgramada>();
        }

        public async Task<IActionResult> OnPostDescargarAsync()
        {
            string ruc = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";
            
            if (string.IsNullOrEmpty(ruc))
            {
                Mensaje = "Seleccione una empresa desde el menu principal.";
                CargarEmpresas();
                CargarDescargasProgramadas();
                return Page();
            }

            CargarEmpresas();

            var empresa = EmpresasSRI.FirstOrDefault(e => e.ruc == ruc);
            if (empresa == null || string.IsNullOrEmpty(empresa.clave))
            {
                Mensaje = "La empresa seleccionada no tiene clave SRI configurada.";
                CargarDescargasProgramadas();
                return Page();
            }

            bool descargarRecibidos = Request.Form["DescargarRecibidos"].Contains("true");
            bool descargarEmitidos = Request.Form["DescargarEmitidos"].Contains("true");
            bool facturas = Request.Form["Facturas"].Contains("true");
            bool notasCredito = Request.Form["NotasCredito"].Contains("true");
            bool notasDebito = Request.Form["NotasDebito"].Contains("true");
            bool retenciones = Request.Form["Retenciones"].Contains("true");
            bool autoImportar = Request.Form["AutoImportar"].Contains("true");

            if (!descargarRecibidos && !descargarEmitidos)
            {
                Mensaje = "Seleccione al menos Recibidos o Emitidos.";
                CargarDescargasProgramadas();
                return Page();
            }

            if (!facturas && !notasCredito && !notasDebito && !retenciones)
            {
                Mensaje = "Seleccione al menos un tipo de documento.";
                CargarDescargasProgramadas();
                return Page();
            }

            Ejecutando = true;
            Resultado = new List<string>();
            Resultado.Add("=== INICIANDO DESCARGA ===");
            Resultado.Add($"Empresa: {empresa.empresa} ({ruc})");
            Resultado.Add($"Anio: {Anio}, Mes: {Mes}");

            var downloadDir = @"C:\descargasSRI\temp";
            var chromePath = @"C:\Users\patri\Downloads\SRI\chromium\chrome.exe";
            var chromedriverPath = @"C:\Users\patri\Downloads\SRI\drivers\chromedriver.exe";

            try
            {
                Resultado.Add($"Iniciando descarga para: {empresa.empresa}...");
                Mensaje = $"Descargando para {empresa.empresa}...";

                using var service = new SriDownloadService(downloadDir, chromePath, chromedriverPath);

                service.OnLog += (msg) =>
                {
                    Resultado.Add(msg);
                };

                var options = new SriDownloadOptions
                {
                    Anio = Anio,
                    Mes = Mes,
                    FechaDesde = FechaDesde,
                    FechaHasta = FechaHasta,
                    DescargarRecibidos = descargarRecibidos,
                    DescargarEmitidos = descargarEmitidos,
                    Facturas = facturas,
                    NotasCredito = notasCredito,
                    NotasDebito = notasDebito,
                    Retenciones = retenciones,
                    Empresa = empresa.empresa
                };

                await service.DescargarAsync(ruc, empresa.clave, options);

                Resultado.Add($"Descarga completada para: {empresa.empresa}");

                // Mostrar resumen
                var s = service.Summary;
                Resultado.Add("");
                Resultado.Add("=== RESUMEN DE DESCARGA ===");
                if (descargarEmitidos)
                    Resultado.Add($"Emitidos: {s.FacturasEmitidas} facturas, {s.NCEmitidas} NC, {s.NDEmitidas} ND, {s.RetEmitidas} retenciones");
                if (descargarRecibidos)
                    Resultado.Add($"Recibidos: {s.FacturasRecibidas} facturas, {s.NCRecibidas} NC, {s.NDRecibidas} ND, {s.RetRecibidas} retenciones");
                Resultado.Add($"TOTAL: {s.Total} documentos descargados");

                // Auto-importar XMLs descargados (solo si esta habilitado)
                int xmlsImportados = 0;
                int xmlsSaltados = 0;

                if (autoImportar)
                {
                    var dirBase = @"C:\descargasSRI";

                    if (descargarEmitidos)
                    {
                        var dirEmitidos = Path.Combine(dirBase, ruc, "EMITIDOS", $"{Anio}-{Mes:00}");
                        if (Directory.Exists(dirEmitidos))
                        {
                            var xmlCount = Directory.GetFiles(dirEmitidos, "*.xml").Length;
                            Resultado.Add($"");
                            Resultado.Add($"Importando XMLs Emitidos desde {Anio}-{Mes:00}...");
                            var importer = new XmlBatchImporter(_db, new ATSXmlGenerator(ruc, empresa.empresa));
                            var res = importer.ImportarDesdeCarpeta(dirEmitidos, "EMITIDOS", ruc);
                            foreach (var r in res)
                                Resultado.Add($"  {r}");
                            var nuevos = res.Count(r => r.Contains("agregado") || r.Contains("nuevo"));
                            xmlsImportados += nuevos;
                            xmlsSaltados += res.Count(r => r.Contains("ya existe"));
                        }
                    }

                    if (descargarRecibidos)
                    {
                        var dirRecibidos = Path.Combine(dirBase, ruc, "RECIBIDOS", $"{Anio}-{Mes:00}");
                        if (Directory.Exists(dirRecibidos))
                        {
                            Resultado.Add($"");
                            Resultado.Add($"Importando XMLs Recibidos desde {Anio}-{Mes:00}...");
                            var importer = new XmlBatchImporter(_db, new ATSXmlGenerator(ruc, empresa.empresa));
                            var res = importer.ImportarDesdeCarpeta(dirRecibidos, "RECIBIDOS", ruc);
                            foreach (var r in res)
                                Resultado.Add($"  {r}");
                            var nuevos = res.Count(r => r.Contains("agregado") || r.Contains("nuevo"));
                            xmlsImportados += nuevos;
                            xmlsSaltados += res.Count(r => r.Contains("ya existe"));
                        }
                    }

                    Resultado.Add("");
                    Resultado.Add($"XMLs importados: {xmlsImportados}, ya existian: {xmlsSaltados}");
                    Mensaje = $"Descarga completada: {s.Total} docs, {xmlsImportados} XMLs importados";
                }
                else
                {
                    Mensaje = $"Descarga completada: {s.Total} docs. Use 'Importar Manual' para importar los XMLs.";
                }
            }
            catch (Exception ex)
            {
                Mensaje = $"Error: {ex.Message}";
                Resultado.Add($"EXCEPTION: {ex.Message}");
            }
            finally
            {
                Ejecutando = false;
            }

            CargarDescargasProgramadas();
            return Page();
        }

        public IActionResult OnPostProgramar()
        {
            string ruc = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";
            
            if (string.IsNullOrEmpty(ruc))
            {
                Mensaje = "Seleccione una empresa desde el menu principal.";
                CargarEmpresas();
                CargarDescargasProgramadas();
                return Page();
            }

            CargarEmpresas();
            var empresa = EmpresasSRI.FirstOrDefault(e => e.ruc == ruc);

            if (!FechaProgramada.HasValue)
            {
                Mensaje = "Seleccione fecha y hora para programar.";
                CargarDescargasProgramadas();
                return Page();
            }

            if (FechaProgramada <= DateTime.Now)
            {
                Mensaje = "La fecha programada debe ser futura.";
                CargarDescargasProgramadas();
                return Page();
            }

            var descarga = new DescargaProgramada
            {
                Id = 1,
                Empresa = empresa?.empresa ?? ruc,
                FechaProgramada = FechaProgramada.Value,
                Estado = "Pendiente"
            };

            DescargasProgramadas.Add(descarga);

            Mensaje = $"Descarga programada para el {FechaProgramada:dd/MM/yyyy HH:mm}";
            CargarDescargasProgramadas();
            return Page();
        }

        public IActionResult OnPostCancelar(int id)
        {
            Mensaje = $"Descarga #{id} cancelada.";
            CargarDescargasProgramadas();
            return Page();
        }

        public async Task<IActionResult> OnPostImportarManualAsync()
        {
            string ruc = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";
            
            if (string.IsNullOrEmpty(ruc))
            {
                Mensaje = "Seleccione una empresa desde el menu principal.";
                return Page();
            }

            CargarEmpresas();
            var empresa = EmpresasSRI.FirstOrDefault(e => e.ruc == ruc);
            if (empresa == null)
            {
                Mensaje = "Empresa no encontrada.";
                return Page();
            }

            Resultado = new List<string>();
            Resultado.Add("=== IMPORTAR XMLs MANUALMENTE ===");
            Resultado.Add($"Empresa: {empresa.empresa} ({ruc})");
            Resultado.Add($"Periodo: {Anio}-{Mes:00}");
            Resultado.Add("");

            var dirBase = @"C:\descargasSRI";
            int xmlsImportados = 0;
            int xmlsSaltados = 0;

            // Importar Emitidos
            var dirEmitidos = Path.Combine(dirBase, ruc, "EMITIDOS", $"{Anio}-{Mes:00}");
            if (Directory.Exists(dirEmitidos))
            {
                Resultado.Add($"Importando XMLs Emitidos...");
                var importer = new XmlBatchImporter(_db, new ATSXmlGenerator(ruc, empresa.empresa));
                var res = importer.ImportarDesdeCarpeta(dirEmitidos, "EMITIDOS", ruc);
                foreach (var r in res)
                    Resultado.Add($"  {r}");
                var nuevos = res.Count(r => r.Contains("agregado") || r.Contains("nuevo"));
                xmlsImportados += nuevos;
                xmlsSaltados += res.Count(r => r.Contains("ya existe"));
            }
            else
            {
                Resultado.Add($"  No se encontro carpeta Emitidos: {dirEmitidos}");
            }

            // Importar Recibidos
            var dirRecibidos = Path.Combine(dirBase, ruc, "RECIBIDOS", $"{Anio}-{Mes:00}");
            if (Directory.Exists(dirRecibidos))
            {
                Resultado.Add($"");
                Resultado.Add($"Importando XMLs Recibidos...");
                var importer = new XmlBatchImporter(_db, new ATSXmlGenerator(ruc, empresa.empresa));
                var res = importer.ImportarDesdeCarpeta(dirRecibidos, "RECIBIDOS", ruc);
                foreach (var r in res)
                    Resultado.Add($"  {r}");
                var nuevos = res.Count(r => r.Contains("agregado") || r.Contains("nuevo"));
                xmlsImportados += nuevos;
                xmlsSaltados += res.Count(r => r.Contains("ya existe"));
            }
            else
            {
                Resultado.Add($"  No se encontro carpeta Recibidos: {dirRecibidos}");
            }

            Resultado.Add("");
            Resultado.Add($"XMLs importados: {xmlsImportados}, ya existian: {xmlsSaltados}");
            Mensaje = $"Importacion completada: {xmlsImportados} nuevos, {xmlsSaltados} ya existian";

            CargarDescargasProgramadas();
            return Page();
        }

        [BindProperty]
        public List<IFormFile> XmlFiles { get; set; } = new();

        public async Task<IActionResult> OnPostImportarDesdeCarpetaAsync()
        {
            string ruc = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";
            
            if (string.IsNullOrEmpty(ruc))
            {
                Mensaje = "Seleccione una empresa desde el menu principal.";
                return Page();
            }

            CargarEmpresas();
            var empresa = EmpresasSRI.FirstOrDefault(e => e.ruc == ruc);
            if (empresa == null)
            {
                Mensaje = "Empresa no encontrada.";
                return Page();
            }

            string contextoCarga = Request.Form["ContextoCarga"];
            if (string.IsNullOrEmpty(contextoCarga))
                contextoCarga = "EMITIDOS";

            Resultado = new List<string>();
            Resultado.Add("=== IMPORTAR XMLs DESDE CARPETA ===");
            Resultado.Add($"Empresa: {empresa.empresa} ({ruc})");
            Resultado.Add($"Contexto: {contextoCarga}");
            Resultado.Add("");

            if (XmlFiles == null || !XmlFiles.Any())
            {
                Resultado.Add("No se seleccionaron archivos.");
                Mensaje = "No se seleccionaron archivos XML.";
                return Page();
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"XML_IMPORT_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempPath);

            int archivosProcesados = 0;
            foreach (var file in XmlFiles)
            {
                if (!file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var filePath = Path.Combine(tempPath, Path.GetFileName(file.FileName));
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
                archivosProcesados++;
            }

            if (archivosProcesados > 0)
            {
                Resultado.Add($"Archivos copiados a temporales: {archivosProcesados}");
                
                var importer = new XmlBatchImporter(_db, new ATSXmlGenerator(ruc, empresa.empresa));
                var res = importer.ImportarDesdeCarpeta(tempPath, contextoCarga, ruc);
                
                foreach (var r in res)
                    Resultado.Add($"  {r}");

                var nuevos = res.Count(r => r.Contains("agregado") || r.Contains("nuevo"));
                var existentes = res.Count(r => r.Contains("ya existe"));

                Resultado.Add("");
                Resultado.Add($"XMLs importados: {nuevos}, ya existian: {existentes}");
                Mensaje = $"Importacion completada: {nuevos} nuevos, {existentes} ya existian";
            }
            else
            {
                Resultado.Add("No se encontraron archivos XML para importar.");
                Mensaje = "No se encontraron archivos XML.";
            }

            // Limpiar
            try { Directory.Delete(tempPath, true); } catch { }

            CargarDescargasProgramadas();
            return Page();
        }

        public IActionResult OnPostGuardarClave(string ruc, string clave)
        {
            try
            {
                string configPath = @"C:\Users\patri\Downloads\SRI\config\config.yaml";
                
                if (!System.IO.File.Exists(configPath))
                {
                    return new JsonResult(new { success = false, message = "Archivo de configuracion no encontrado" });
                }

                var lineas = System.IO.File.ReadAllLines(configPath).ToList();
                bool encontrado = false;

                for (int i = 0; i < lineas.Count; i++)
                {
                    var trimmed = lineas[i].Trim();
                    
                    if (trimmed.StartsWith("- ruc:") && trimmed.Contains(ruc))
                    {
                        int idxRuc = i;
                        int idxClave = -1;
                        
                        for (int j = i + 1; j < lineas.Count && j <= i + 5; j++)
                        {
                            if (lineas[j].Trim().StartsWith("- "))
                                break;
                            if (lineas[j].Trim().StartsWith("clave:"))
                            {
                                idxClave = j;
                                break;
                            }
                        }

                        if (idxClave >= 0)
                        {
                            var parteClave = lineas[idxClave].Split(new[] { "clave:" }, StringSplitOptions.None)[0];
                            lineas[idxClave] = parteClave + "clave: \"" + clave + "\"";
                            encontrado = true;
                            break;
                        }
                    }
                }

                if (!encontrado)
                {
                    for (int i = 0; i < lineas.Count; i++)
                    {
                        if (lineas[i].Contains("ruc:") && lineas[i].Contains(ruc))
                        {
                            for (int j = i; j < lineas.Count && j < i + 5; j++)
                            {
                                if (lineas[j].Contains("clave:"))
                                {
                                    lineas[j] = "    clave: \"" + clave + "\"";
                                    encontrado = true;
                                    break;
                                }
                            }
                            if (encontrado) break;
                        }
                    }
                }

                if (encontrado)
                {
                    System.IO.File.WriteAllLines(configPath, lineas);
                    return new JsonResult(new { success = true, message = "Clave actualizada" });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "Empresa no encontrada en config" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostLimpiarYReimportarAsync()
        {
            string ruc = HttpContext.Session.GetString("EmpresaSeleccionada") ?? "";
            
            if (string.IsNullOrEmpty(ruc))
            {
                Mensaje = "Seleccione una empresa desde el menu principal.";
                return Page();
            }

            CargarEmpresas();
            var empresa = EmpresasSRI.FirstOrDefault(e => e.ruc == ruc);
            if (empresa == null)
            {
                Mensaje = "Empresa no encontrada.";
                return Page();
            }

            Resultado = new List<string>();
            Resultado.Add("=== LIMPIAR Y REIMPORTAR ===");
            Resultado.Add($"Empresa: {empresa.empresa} ({ruc})");
            Resultado.Add($"Periodo: {Anio}-{Mes:00}");
            Resultado.Add("");

            var periodoInicio = new DateTime(Anio, Mes, 1);
            var periodoFin = periodoInicio.AddMonths(1).AddDays(-1);

            // 1. Eliminar registros existentes del período
            try
            {
                int ventasEliminadas = await _db.Ventas
                    .Where(v => v.RucEmpresa == ruc && v.FechaEmision >= periodoInicio && v.FechaEmision <= periodoFin)
                    .ExecuteDeleteAsync();
                Resultado.Add($"Ventas eliminadas: {ventasEliminadas}");

                int comprasEliminadas = await _db.Compras
                    .Where(c => c.RucEmpresa == ruc && c.FechaEmision >= periodoInicio && c.FechaEmision <= periodoFin)
                    .ExecuteDeleteAsync();
                Resultado.Add($"Compras eliminadas: {comprasEliminadas}");

                int retencionesClientesEliminadas = await _db.RetencionesClientes
                    .Where(r => r.RucEmpresa == ruc && r.FechaRetencion >= periodoInicio && r.FechaRetencion <= periodoFin)
                    .ExecuteDeleteAsync();
                Resultado.Add($"Retenciones Clientes eliminadas: {retencionesClientesEliminadas}");
                
                int retencionesComprasEliminadas = await _db.RetencionesCompras
                    .Where(r => r.RucEmpresa == ruc && r.FechaRetencion >= periodoInicio && r.FechaRetencion <= periodoFin)
                    .ExecuteDeleteAsync();
                Resultado.Add($"Retenciones Compras eliminadas: {retencionesComprasEliminadas}");

                Resultado.Add("");
                Resultado.Add("Registros eliminados correctamente. Iniciando reimportación...");
            }
            catch (Exception ex)
            {
                Resultado.Add($"ERROR al eliminar registros: {ex.Message}");
                Mensaje = "Error al limpiar registros.";
                return Page();
            }

            // 2. Reimportar EMITIDOS
            var dirBase = @"C:\descargasSRI";
            var dirEmitidos = Path.Combine(dirBase, ruc, "EMITIDOS", $"{Anio}-{Mes:00}");
            if (Directory.Exists(dirEmitidos))
            {
                Resultado.Add("");
                Resultado.Add($"Importando Emitidos: {dirEmitidos}");
                var importer = new XmlBatchImporter(_db, new ATSXmlGenerator(ruc, empresa.empresa));
                var res = importer.ImportarDesdeCarpeta(dirEmitidos, "EMITIDOS", ruc);
                foreach (var r in res)
                    Resultado.Add($"  {r}");
            }
            else
            {
                Resultado.Add($"No se encontró carpeta Emitidos: {dirEmitidos}");
            }

            // 3. Reimportar RECIBIDOS
            var dirRecibidos = Path.Combine(dirBase, ruc, "RECIBIDOS", $"{Anio}-{Mes:00}");
            if (Directory.Exists(dirRecibidos))
            {
                Resultado.Add("");
                Resultado.Add($"Importando Recibidos: {dirRecibidos}");
                var importer = new XmlBatchImporter(_db, new ATSXmlGenerator(ruc, empresa.empresa));
                var res = importer.ImportarDesdeCarpeta(dirRecibidos, "RECIBIDOS", ruc);
                foreach (var r in res)
                    Resultado.Add($"  {r}");
            }
            else
            {
                Resultado.Add($"No se encontró carpeta Recibidos: {dirRecibidos}");
            }

            Resultado.Add("");
            Resultado.Add("=== REIMPORTACIÓN COMPLETADA ===");
            Mensaje = "Periodo limpiado y reimportado correctamente.";
            return Page();
        }

    }
    
    public class EmpresaSRI
    {
        public string ruc { get; set; } = "";
        public string empresa { get; set; } = "";
        public string clave { get; set; } = "";
    }
}
