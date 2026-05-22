using AtsManager.Pages.Empresas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace AtsManager.Pages.Ventas
{
    public class CargaLotesModel : PageModel
    {
        private readonly AtsDbContext _db;

        public CargaLotesModel(AtsDbContext context)
        {
            _db = context;
        }

        // -----------------------------------------------------
        // Propiedades para la Vista (Binding)
        // -----------------------------------------------------
        
        [BindProperty]
        public IFormFile ArchivoVentas { get; set; } = default!;

        [BindProperty]
        [Required(ErrorMessage = "Debe ingresar el RUC de la empresa emisora (su empresa).")]
        public string RucEmisor { get; set; } = default!;

        [TempData]
        public string MensajeProceso { get; set; } = string.Empty;
        [BindProperty] // NECESARIO para que el formulario la llene
        [Required(ErrorMessage = "Debe seleccionar un archivo para cargar.")]
        public IFormFile ArchivoCarga { get; set; } = default!; // ¡DECLARACIÓN CORREGIDA!

        [BindProperty]
        [Required(ErrorMessage = "Debe seleccionar un tipo de anexo.")]
        public string TipoArchivo { get; set; } = default!;

        [BindProperty]
        public int Anio { get; set; }

        [BindProperty]
        public int Mes { get; set; }
        // -----------------------------------------------------
        // Manejadores (Handers)
        // -----------------------------------------------------

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostProcessAsync()
        {
            // =================================================================
            // 1. LIMPIEZA CONDICIONAL DEL MODEL STATE
            // =================================================================
            // Esto es NECESARIO porque sus modelos (Venta, Compra, Retencion) tienen propiedades requeridas
            // (non-nullable) que NO están en el formulario HTML. El framework intenta validarlas
            // con NULL, haciendo que ModelState.IsValid sea FALSO.

            // 1.1. Limpieza de Propiedades del PageModel que no se usan siempre
            ModelState.Remove("RucEmisor");

            // 1.2. Limpieza de Propiedades de Entidades que serán asignadas luego

            // Limpieza General (propiedades non-nullable que nunca vienen en el formulario)
            ModelState.Remove("ClaveAcceso");
            ModelState.Remove("FechaAutorizacion");
            ModelState.Remove("BaseNoGraIva");
            ModelState.Remove("MontoTotal");

            // Lógica para limpiar propiedades que pertenecen a OTRAS ENTIDADES
            // No podemos saber qué entidad está fallando si no sabemos qué procesamos.

            if (TipoArchivo == "VENTAS")
            {
                // Limpiamos campos de Venta que se derivan del archivo, no del formulario
                ModelState.Remove("Estab");
                ModelState.Remove("PtoEmi");
                ModelState.Remove("Secuencial");
                ModelState.Remove("NumComprobante");
                ModelState.Remove("IdCliente");
                ModelState.Remove("RazonSocialCliente");
                ModelState.Remove("valRetIVA");
                ModelState.Remove("valRetRenta");
                ModelState.Remove("NumRetencion");
            }
            else if (TipoArchivo == "COMPRAS")
            {
                // Limpiamos campos específicos de Compra (ej. si su entidad Compra requiere un código sustento)
                // Ejemplo: ModelState.Remove("CodSustento");
                // Ejemplo: ModelState.Remove("TipoSustento"); 
            }
            else if (TipoArchivo == "RETENCIONES")
            {
                // Limpiamos campos específicos de Retención
                // Ejemplo: ModelState.Remove("NumRetencion");
                // Ejemplo: ModelState.Remove("EjercicioFiscal");
            }

            // =================================================================
            // 2. VERIFICACIÓN DE VALIDACIÓN DEL MODELO
            // =================================================================
            if (!ModelState.IsValid || ArchivoCarga == null)
            {
                // Si falla aquí, significa que faltó un campo (Anio, Mes, TipoArchivo) o el archivo.
                MensajeProceso = "❌ Error: Debe seleccionar el Año, Mes, Tipo de Anexo y un Archivo válido.";
                return Page();
            }

            // =================================================================
            // 3. INICIO DE PROCESAMIENTO Y LOTE MAESTRO
            // =================================================================
            // NOTA: El TipoDocumento en CargaLote se asigna con el valor bindeado (TipoArchivo)
            var nuevoLote = new CargaLote
            {
                TipoDocumento = TipoArchivo, // ✅ ¡Aquí se resuelve el error NULL!
                FechaCarga = DateTime.Now,
                NombreArchivo = ArchivoCarga.FileName,
                Anio = Anio,
                Mes = Mes,
                // ... (otros campos de auditoría)
            };
            _db.CargasLotes.Add(nuevoLote);
            await _db.SaveChangesAsync(); // Guardamos para obtener el ID del Lote

            int lineasProcesadas = 0;
            var culturaEcuatoriana = new CultureInfo("es-EC"); // Asumiendo cultura ecuatoriana
            var ventasCargadas = new List<Venta>();
            // var comprasCargadas = new List<Compra>();
            // var retencionesCargadas = new List<Retencion>();

            try
            {
                using (var reader = new StreamReader(ArchivoCarga.OpenReadStream(), Encoding.UTF8))
                {
                    string line;
                    bool esCabecera = true;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (esCabecera || string.IsNullOrWhiteSpace(line))
                        {
                            esCabecera = false;
                            continue;
                        }

                        // Asumimos que el archivo es delimitado por tabulaciones ('\t')
                        var campos = line.Split('\t');

                        // =================================================================
                        // 4. LÓGICA DE PROCESAMIENTO CONDICIONAL
                        // =================================================================
                        if (TipoArchivo == "VENTAS")
                        {
                            // Lógica para archivos de VENTA (su código de ejemplo)
                            if (campos.Length < 8) continue;

                            var numComprobante = campos[1]; // Ej: "001-002-000000900"
                            var partesComprobante = numComprobante.Split('-');
                            var claveAcceso = campos[2];

                            if (!DateTime.TryParseExact(campos[4].Split(' ')[0], "dd/MM/yyyy", culturaEcuatoriana, DateTimeStyles.None, out DateTime fechaEmision))
                            {
                                MensajeProceso = $"❌ Error de fecha en línea {lineasProcesadas + 2}. Formato esperado: dd/MM/yyyy";
                                throw new Exception("Error de formato de fecha.");
                            }

                            if (!decimal.TryParse(campos[5], NumberStyles.Currency, culturaEcuatoriana, out decimal baseImponible) ||
                                !decimal.TryParse(campos[6], NumberStyles.Currency, culturaEcuatoriana, out decimal montoIva))
                            {
                                MensajeProceso = $"❌ Error en valores numéricos en línea {lineasProcesadas + 2}. Verifique el formato decimal.";
                                throw new Exception("Error de formato numérico.");
                            }

                            var nuevaVenta = new Venta
                            {
                                CargaLoteId = nuevoLote.Id,
                                TipoComprobante = campos[0],
                                NumComprobante = numComprobante,

                                // **Asignación de partes del comprobante (solución al ModelState.Remove())**
                                Estab = partesComprobante[0],
                                PtoEmi = partesComprobante[1],
                                Secuencial = partesComprobante[2],

                                ClaveAcceso = claveAcceso,
                                FechaEmision = fechaEmision,
                                Anio = (short)fechaEmision.Year,
                                Mes = (short)fechaEmision.Month,
                                BaseImponible = baseImponible,
                                MontoIva = montoIva,
                                // ... (otras asignaciones)
                            };
                            ventasCargadas.Add(nuevaVenta);
                        }
                        else if (TipoArchivo == "COMPRAS")
                        {
                            // Lógica de lectura para archivos de COMPRAS
                            // EJEMPLO: var nuevaCompra = new Compra { /* ... mapeo de campos de compra ... */ };
                            // EJEMPLO: comprasCargadas.Add(nuevaCompra);
                        }
                        else if (TipoArchivo == "RETENCIONES")
                        {
                            // Lógica de lectura para archivos de RETENCIONES
                            // EJEMPLO: var nuevaRetencion = new Retencion { /* ... mapeo de campos de retencion ... */ };
                            // EJEMPLO: retencionesCargadas.Add(nuevaRetencion);
                        }
                        

                        lineasProcesadas++;
                    }
                }

                // =================================================================
                // 5. GUARDADO DE DATOS
                // =================================================================
                if (TipoArchivo == "VENTAS" && ventasCargadas.Any())
                {
                    _db.Ventas.AddRange(ventasCargadas);
                }
                // else if (TipoArchivo == "COMPRAS" && comprasCargadas.Any()) { _db.Compras.AddRange(comprasCargadas); }
                // else if (TipoArchivo == "RETENCIONES" && retencionesCargadas.Any()) { _db.Retenciones.AddRange(retencionesCargadas); }

                await _db.SaveChangesAsync();

                MensajeProceso = $"✅ ÉXITO: Se cargó el lote #{nuevoLote.Id} con {lineasProcesadas} registros de {TipoArchivo}.";
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                // Manejo de errores: Eliminar el lote maestro y los registros parciales
                _db.Ventas.RemoveRange(ventasCargadas);
                // _db.Compras.RemoveRange(comprasCargadas);
                // _db.Retenciones.RemoveRange(retencionesCargadas);
                _db.CargasLotes.Remove(nuevoLote);
                await _db.SaveChangesAsync();

                MensajeProceso = $"❌ Error fatal durante la carga del archivo de {TipoArchivo}: {ex.Message}";
                return Page();
            }
        }
    }
}