using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AtsManager.Pages.Descargas
{
    public class EmpresaSRI
    {
        public string ruc { get; set; } = "";
        public string empresa { get; set; } = "";
        public string clave { get; set; } = "";
    }

    public class ConfigSRI
    {
        public List<EmpresaSRI> rucs { get; set; } = new List<EmpresaSRI>();
    }

    public class IndexModel : PageModel
    {
        [BindProperty]
        public int Anio { get; set; } = DateTime.Now.Year;

        [BindProperty]
        public int Mes { get; set; } = DateTime.Now.Month;

        [BindProperty]
        public string Modo { get; set; } = "ambos";

        [BindProperty]
        public List<string> EmpresasSeleccionadas { get; set; } = new List<string>();

        public List<EmpresaSRI> EmpresasSRI { get; set; } = new List<EmpresaSRI>();

        public string Mensaje { get; set; } = "";

        public bool Ejecutando { get; set; } = false;

        public List<string> Resultado { get; set; } = new List<string>();

        public void OnGet()
        {
            CargarEmpresasSRI();
        }

        private void CargarEmpresasSRI()
        {
            try
            {
                string configPath = @"C:\Users\patri\Downloads\SRI\config\config.yaml";
                if (System.IO.File.Exists(configPath))
                {
                    var deserializer = new DeserializerBuilder()
                        .IgnoreUnmatchedProperties()
                        .Build();

                    var yaml = System.IO.File.ReadAllText(configPath);
                    var config = deserializer.Deserialize<ConfigSRI>(yaml);
                    
                    if (config?.rucs != null)
                    {
                        foreach (var r in config.rucs)
                        {
                            EmpresasSRI.Add(r);
                        }
                    }
                }
                else
                {
                    Mensaje = "⚠️ No se encontró el archivo de configuración en: " + configPath;
                }
            }
            catch (Exception ex)
            {
                Mensaje = $"Error al cargar configuración: {ex.Message}";
            }
        }

        public async Task<IActionResult> OnPostDescargarAsync()
        {
            CargarEmpresasSRI();
            
            // Validar que se haya seleccionado al menos un RUC
            if (EmpresasSeleccionadas == null || EmpresasSeleccionadas.Count == 0)
            {
                Mensaje = "⚠️ Seleccione al menos una empresa.";
                return Page();
            }

            Ejecutando = true;

            try
            {
                string pythonPath = "python";
                string scriptPath = @"C:\Users\patri\Downloads\SRI\cli.py";
                string mesStr = $"{Anio}-{Mes:D2}";

                // Construir argumentos con los RUCs seleccionados
                string rucsArg = "";
                if (EmpresasSeleccionadas != null && EmpresasSeleccionadas.Count > 0)
                {
                    rucsArg = "--rucs " + string.Join(",", EmpresasSeleccionadas);
                }

                var args = $"\"{scriptPath}\" --month {mesStr} --modo {Modo} {rucsArg}";
                
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = args.Trim(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = @"C:\Users\patri\Downloads\SRI",
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                
                // Forzar UTF-8 para evitar errores de codificacion
                processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = Task.Run(() =>
                {
                    var lines = new List<string>();
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            lines.Add(line);
                        }
                    }
                    return lines;
                });

                var errorTask = Task.Run(() =>
                {
                    var lines = new List<string>();
                    while (!process.StandardError.EndOfStream)
                    {
                        var line = process.StandardError.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            lines.Add(line);
                        }
                    }
                    return lines;
                });

                await process.WaitForExitAsync();

                var output = await outputTask;
                var errors = await errorTask;

                Resultado = new List<string>();
                Resultado.AddRange(output);
                if (errors.Count > 0)
                {
                    Resultado.Add("--- ERRORES ---");
                    Resultado.AddRange(errors);
                }

                if (process.ExitCode == 0)
                {
                    Mensaje = "✅ Descarga completada exitosamente";
                }
                else
                {
                    Mensaje = $"❌ La descarga terminó con errores (código: {process.ExitCode})";
                }
            }
            catch (Exception ex)
            {
                Mensaje = $"❌ Error al ejecutar: {ex.Message}";
                Resultado.Add(ex.ToString());
            }
            finally
            {
                Ejecutando = false;
            }

            return Page();
        }
    }
}
