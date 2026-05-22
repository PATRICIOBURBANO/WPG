using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SeleniumExtras.WaitHelpers;
using static OpenQA.Selenium.StaleElementReferenceException;
using static OpenQA.Selenium.ElementNotInteractableException;
using static OpenQA.Selenium.ElementClickInterceptedException;

namespace AtsManager.Services
{
    public class SriDownloadOptions
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public bool DescargarRecibidos { get; set; } = true;
        public bool DescargarEmitidos { get; set; } = true;
        public bool Facturas { get; set; } = true;
        public bool NotasCredito { get; set; } = true;
        public bool NotasDebito { get; set; } = true;
        public bool Retenciones { get; set; } = true;
        public string Empresa { get; set; } = "";
    }

    public class DownloadSummary
    {
        public int FacturasEmitidas { get; set; }
        public int NCEmitidas { get; set; }
        public int NDEmitidas { get; set; }
        public int RetEmitidas { get; set; }
        public int FacturasRecibidas { get; set; }
        public int NCRecibidas { get; set; }
        public int NDRecibidas { get; set; }
        public int RetRecibidas { get; set; }
        public int Total => FacturasEmitidas + NCEmitidas + NDEmitidas + RetEmitidas + FacturasRecibidas + NCRecibidas + NDRecibidas + RetRecibidas;
        public string ResumenTexto { get; set; } = "";
    }

    public class SriDownloadService : IDisposable
    {
        private ChromeDriver? _driver;
        private WebDriverWait? _wait;
        private string _downloadDir;
        private string _chromePath;
        private string _chromedriverPath;
        private string? _tempProfileDir;
        private bool _disposed;
        private string _rucLimpio = "";
        private string _empresaNombre = "";
        private bool _esRecibidos = false;

        private string GetPrimeraPalabra(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            var palabras = texto.Trim().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return palabras.Length > 0 ? palabras[0] : "";
        }

        public event Action<string>? OnLog;
        public event Action<int, int>? OnProgress;
        public event Action<DownloadSummary>? OnSummary;

        public DownloadSummary Summary { get; private set; } = new DownloadSummary();

        private const int SELENIUM_TIMEOUT = 10;

        public SriDownloadService(
            string downloadDir,
            string chromePath = @"C:\Users\patri\Downloads\SRI\chromium\chrome.exe",
            string chromedriverPath = @"C:\Users\patri\Downloads\SRI\drivers\chromedriver.exe")
        {
            _downloadDir = downloadDir;
            _chromePath = chromePath;
            _chromedriverPath = chromedriverPath;
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        private void SafeClick(IWebElement element)
        {
            try
            {
                ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element);
                Thread.Sleep(5);
                ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].click();", element);
            }
            catch (Exception)
            {
                try { element.Click(); } catch { }
            }
        }

        private string Extract(string input, string pattern)
        {
            var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalizedString)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().ToLower().Trim();
        }

        public async Task DescargarAsync(
            string ruc,
            string clave,
            SriDownloadOptions options,
            CancellationToken cancellationToken = default)
        {
            _rucLimpio = ruc.Replace("'", "").Replace("\"", "").Trim();
            _empresaNombre = options.Empresa;
            
            await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(_downloadDir);
                    LimpiarDescargas();

                    Log("=== INICIANDO DESCARGA ===");
                    Log($"RUC: {_rucLimpio} | Periodo: {options.Anio}-{options.Mes:00}");

                    InicializarDriver();
                    Login(_rucLimpio, clave);

                    var dirRecibidos = Path.Combine(@"C:\descargasSRI", _rucLimpio, "RECIBIDOS", $"{options.Anio}-{options.Mes:00}");
                    var dirEmitidos = Path.Combine(@"C:\descargasSRI", _rucLimpio, "EMITIDOS", $"{options.Anio}-{options.Mes:00}");
                    Directory.CreateDirectory(dirRecibidos);
                    Directory.CreateDirectory(dirEmitidos);
                    Log($"Recibidos: {dirRecibidos}");
                    Log($"Emitidos: {dirEmitidos}");

                    if (options.DescargarRecibidos && !options.DescargarEmitidos)
                    {
                        Log(">>> PROCESANDO SOLO RECIBIDOS");
                        DescargarRecibidos(options, dirRecibidos);
                    }
                    else if (options.DescargarEmitidos && !options.DescargarRecibidos)
                    {
                        Log(">>> PROCESANDO SOLO EMITIDOS");
                        DescargarEmitidos(options, dirEmitidos);
                    }
                    else if (options.DescargarRecibidos && options.DescargarEmitidos)
                    {
                        Log(">>> PROCESANDO RECIBIDOS Y EMITIDOS");
                        DescargarRecibidos(options, dirRecibidos);
                        Log(">>> Cambiando a EMITIDOS...");
                        _driver.SwitchTo().DefaultContent();
                        CerrarPopup();
                        CerrarDialogosPrimeraVez();
                        Thread.Sleep(500);
                        DescargarEmitidos(options, dirEmitidos);
                    }
                    else
                    {
                        Log("ERROR: No se especifico Recibidos ni Emitidos");
                    }

                    Log("=== DESCARGA COMPLETADA ===");
                    Summary.ResumenTexto = $"Emitidos: {Summary.FacturasEmitidas} facturas, {Summary.NCEmitidas} NC, {Summary.NDEmitidas} ND, {Summary.RetEmitidas} retenciones";
                    Summary.ResumenTexto += $" | Recibidos: {Summary.FacturasRecibidas} facturas, {Summary.NCRecibidas} NC, {Summary.NDRecibidas} ND, {Summary.RetRecibidas} retenciones";
                    Summary.ResumenTexto += $" | TOTAL: {Summary.Total} documentos";
                    OnSummary?.Invoke(Summary);
                }
                catch (Exception ex)
                {
                    Log($"ERROR GENERAL: {ex.Message}");
                }
                finally
                {
                    CerrarDriver();
                }
            }, cancellationToken);
        }

        private void LimpiarDescargas()
        {
            try
            {
                foreach (var f in Directory.GetFiles(_downloadDir))
                {
                    var ext = Path.GetExtension(f).ToLower();
                    if (ext == ".xml" || ext == ".crdownload" || ext == ".tmp" || ext == ".part")
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        private void InicializarDriver()
        {
            Log("Inicializando Chrome...");

            var options = new ChromeOptions();
            options.BinaryLocation = _chromePath;

            // Crear directorio temporal para perfil (como Python hace)
            _tempProfileDir = Path.Combine(Path.GetTempPath(), $"sri_profile_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempProfileDir);
            Log($"Perfil temporal: {_tempProfileDir}");
            options.AddArgument($"--user-data-dir={_tempProfileDir}");

            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-gpu-sandbox");
            options.AddArgument("--use-gl=swiftshader");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-features=TranslateUI");
            options.AddArgument("--disable-background-networking");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-renderer-backgrounding");
            options.AddArgument("--disable-infobars");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--ignore-ssl-errors");
            options.AddArgument("--disable-setuid-sandbox");
            options.AddArgument("--disable-logging");
            options.AddArgument("--log-level=3");

            options.AddUserProfilePreference("download.default_directory", _downloadDir);
            options.AddUserProfilePreference("download.prompt_for_download", false);
            options.AddUserProfilePreference("download.directory_upgrade", true);
            options.AddUserProfilePreference("profile.default_content_setting_values.automatic_downloads", 1);

            var driverDir = Path.GetDirectoryName(_chromedriverPath) ?? "";
            Log($"Driver dir: {driverDir}");

            var driverService = ChromeDriverService.CreateDefaultService(driverDir);
            driverService.SuppressInitialDiagnosticInformation = true;
            driverService.HideCommandPromptWindow = true;

            try
            {
                _driver = new ChromeDriver(driverService, options);
                _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
                _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

                _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(SELENIUM_TIMEOUT));

                Log("Driver OK");
            }
            catch (Exception ex)
            {
                Log($"ERROR InicializarDriver: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }

        private void Login(string ruc, string clave)
        {
            try
            {
                var loginUrl = "https://srienlinea.sri.gob.ec/auth/realms/Internet/protocol/openid-connect/auth?client_id=app-sri-claves-angular&redirect_uri=https%3A%2F%2Fsrienlinea.sri.gob.ec%2Fsri-en-linea%2Fcontribuyente%2Fperfil%3Ffaces-redirect%3Dtrue&state=xxx&nonce=xxx&response_mode=fragment&response_type=code&scope=openid";

                _driver!.Navigate().GoToUrl(loginUrl);

                var usernameField = _wait!.Until(ExpectedConditions.ElementToBeClickable(By.Name("usuario")));
                usernameField.Clear();
                usernameField.SendKeys(ruc);

                var passwordField = _driver.FindElement(By.Name("password"));
                passwordField.Clear();
                passwordField.SendKeys(clave);

                ((IJavaScriptExecutor)_driver).ExecuteScript("document.getElementById('kc-login').click();");

                Thread.Sleep(5);
                Log($"Login OK: {_driver.Url}");
            }
            catch (Exception ex)
            {
                Log($"ERROR login: {ex.Message}");
            }
        }

        private void EsperarFinAjax(int timeoutSegundos = 3)
        {
            if (timeoutSegundos <= 0)
            {
                Thread.Sleep(5);
                return;
            }

            var end = DateTime.Now.AddSeconds(timeoutSegundos);
            while (DateTime.Now < end)
            {
                try
                {
                    var listo = (bool)((IJavaScriptExecutor)_driver!).ExecuteScript(@"
                        try {
                            if (window.PrimeFaces && PrimeFaces.ajax) {
                                return PrimeFaces.ajax.Queue.isEmpty();
                            }
                        } catch(e) { }
                        // fallback: buscar spinners/cargando
                        var spinner = document.querySelector('.ui-blockui, .blockUI, .loading');
                        if (spinner && window.getComputedStyle(spinner).display !== 'none') return false;
                        var busy = document.querySelector('[aria-busy=""true""]');
                        if (busy) return false;
                        return true;
                    ");
                    if (listo) return;
                }
                catch { }
                Thread.Sleep(15);
            }
        }

        private void CerrarDialogosPrimeraVez()
        {
            try
            {
                var modales = _driver!.FindElements(By.CssSelector("div.ui-dialog"));
                foreach (var modal in modales)
                {
                    var style = modal.GetAttribute("style") ?? "";
                    if (style.Contains("display: none") || style.Contains("visibility: hidden"))
                        continue;

                    var btnCerrar = modal.FindElements(By.CssSelector(".ui-dialog-titlebar-close"));
                    if (btnCerrar.Count > 0)
                    {
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btnCerrar[0]);
                        Thread.Sleep(5);
                        return;
                    }
                }
            }
            catch { }
        }

        private int _captchaLoopCount = 0;
        private const int MAX_CAPTCHA_LOOP = 5;

        private bool CerrarModalCaptcha()
        {
            try
            {
                var msgs = _driver!.FindElements(By.CssSelector(".ui-message-corner, .ui-message, div.ui-message, .ui-messages-warn"));
                foreach (var msg in msgs)
                {
                    var style = msg.GetAttribute("style") ?? "";
                    if (style.Contains("display: none")) continue;

                    var texto = msg.Text.ToLower();
                    if (texto.Contains("captcha"))
                    {
                        var btnCerrar = msg.FindElements(By.CssSelector(".ui-messages-close, .ui-message-close, .ui-icon-close, button.close"));
                        if (btnCerrar.Count > 0)
                        {
                            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btnCerrar[0]);
                            Thread.Sleep(5);
                            _captchaLoopCount++;
                            Log($"  [CAPTCHA] Detectado ({_captchaLoopCount}/{MAX_CAPTCHA_LOOP})");
                            return true;
                        }
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].style.display='none';", msg);
                        _captchaLoopCount++;
                        return true;
                    }
                }

                var modales = _driver!.FindElements(By.CssSelector("div.ui-dialog"));
                foreach (var modal in modales)
                {
                    var style = modal.GetAttribute("style") ?? "";
                    if (style.Contains("display: none") || style.Contains("visibility: hidden"))
                        continue;

                    var texto = modal.Text.ToLower();
                    if (texto.Contains("captcha"))
                    {
                        var botones = modal.FindElements(By.CssSelector(".ui-dialog-titlebar-close"));
                        if (botones.Count > 0)
                        {
                            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", botones[0]);
                            Thread.Sleep(5);
                            _captchaLoopCount++;
                            Log($"  [CAPTCHA] Modal ({_captchaLoopCount}/{MAX_CAPTCHA_LOOP})");
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private bool VerificarCaptchaBloqueante()
        {
            if (_captchaLoopCount >= MAX_CAPTCHA_LOOP)
            {
                Log($"  [CAPTCHA] BLOQUEO: Demasiados captchas ({_captchaLoopCount}). Refrescando pagina...");
                _driver!.Navigate().Refresh();
                Thread.Sleep(2000);
                _captchaLoopCount = 0;
                return true;
            }
            return false;
        }

        private void ResetCaptchaCount()
        {
            _captchaLoopCount = 0;
        }

        private bool NavegarRecibidos()
        {
            Log("Navegando a Recibidos...");
            _driver!.SwitchTo().DefaultContent();
            Thread.Sleep(5);

            try
            {
                var btnMenu = _wait!.Until(ExpectedConditions.ElementToBeClickable(By.Id("sri-menu")));
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btnMenu);
                Thread.Sleep(1500);
            }
            catch (Exception ex)
            {
                Log($"Error menu: {ex.Message}");
                return false;
            }

            try
            {
                Log("Buscando FACTURACION ELECTRONICA...");
                var facturacion = _wait.Until(ExpectedConditions.ElementExists(
                    By.XPath("//a[.//span[contains(.,'FACTUR')]]")));
                Log("FACTURACION encontrado, clicking...");
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", facturacion);
                Thread.Sleep(2000);

                Log("Buscando Comprobantes electronicos recibidos...");
                
                // Use exact same XPath as Python
                var recibidos = _wait.Until(ExpectedConditions.ElementExists(
                    By.XPath("//a[.//span[contains(.,'Comprobantes')]]")));
                Log("Recibidos encontrado, clicking...");
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", recibidos);
                Thread.Sleep(2000);

                Log("Esperando campo cmbTipoComprobante...");
                _wait.Until(ExpectedConditions.ElementExists(By.Id("frmPrincipal:cmbTipoComprobante")));
                Log("Recibidos listo");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error Recibidos: {ex.Message}");
                return false;
            }
        }

        private Dictionary<string, string> ObtenerTiposSeleccionados(SriDownloadOptions options)
        {
            var tipos = new Dictionary<string, string>();
            if (options.Facturas) tipos["factura"] = "FACTURA";
            if (options.NotasCredito) tipos["notas de credito"] = "NOTA_CREDITO";
            if (options.NotasDebito) tipos["notas de debito"] = "NOTA_DEBITO";
            if (options.Retenciones) tipos["retencion"] = "RETENCION";
            return tipos;
        }

        private void SeleccionarTipoRecibidos(string palabra)
        {
            var claveNorm = NormalizeText(palabra).Replace("_", " ");
            Log($"  Tipo: '{palabra}'");

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    _wait!.Until(ExpectedConditions.ElementExists(By.Id("frmPrincipal:cmbTipoComprobante")));
                    Thread.Sleep(10);
                    
                    var selectEl = _driver!.FindElement(By.Id("frmPrincipal:cmbTipoComprobante"));
                    var select = new SelectElement(selectEl);

                    foreach (var option in select.Options)
                    {
                        var optNorm = NormalizeText(option.Text);
                        if (optNorm.Contains(claveNorm))
                        {
                            select.SelectByText(option.Text);
                            Log($"  Seleccionado: '{option.Text}'");
                            Thread.Sleep(10);
                            return;
                        }
                    }
                    
                    Log($"  Tipo '{palabra}' no encontrado en dropdown");
                    return;
                }
                catch (StaleElementReferenceException)
                {
                    Log($"  Stale element, reintentando ({attempt + 1}/3)...");
                    Thread.Sleep(15);
                }
                catch (Exception ex)
                {
                    Log($"Error tipo (intento {attempt + 1}): {ex.Message}");
                    Thread.Sleep(10);
                }
            }
            
            Log($"  Fallo al seleccionar tipo '{palabra}' despues de 3 intentos");
        }

        private void ClickConsultarRecibidos()
        {
            try
            {
                var btn = _wait!.Until(ExpectedConditions.ElementToBeClickable(
                    By.XPath("//button[.//span[contains(.,'Consultar')]]")));
                ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", btn);
                ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].click();", btn);
                Thread.Sleep(5);
            }
            catch (Exception ex)
            {
                Log($"Error consultar: {ex.Message}");
            }
        }

        private void DescargarRecibidos(SriDownloadOptions options, string dirMes)
        {
            Directory.CreateDirectory(dirMes);
            Log($"Carpeta: {dirMes}");
            ResetCaptchaCount();

            try
            {
                if (!NavegarRecibidos())
                {
                    Log("Error: No se pudo navegar a Recibidos");
                    return;
                }
                Thread.Sleep(10);
                CerrarDialogosPrimeraVez();
                CerrarModalCaptcha();
                LimpiarDescargas();

                Log("Configurando periodo...");
                
                try
                {
                    new SelectElement(_driver!.FindElement(By.Id("frmPrincipal:ano"))).SelectByValue(options.Anio.ToString());
                    Thread.Sleep(10);
                    new SelectElement(_driver.FindElement(By.Id("frmPrincipal:mes"))).SelectByValue(options.Mes.ToString());
                    Thread.Sleep(10);
                    new SelectElement(_driver.FindElement(By.Id("frmPrincipal:dia"))).SelectByValue("0");
                    Thread.Sleep(15);
                }
                catch (Exception ex)
                {
                    Log($"Error configurando periodo: {ex.Message}");
                    return;
                }

                CerrarDialogosPrimeraVez();
                CerrarModalCaptcha();

                var tipos = ObtenerTiposSeleccionados(options);
                if (tipos.Count == 0)
                {
                    Log("Sin tipos seleccionados");
                    return;
                }

                int tipoIndex = 0;
                string tipoAnterior = "";
                
                foreach (var tipo in tipos)
                {
                    if (VerificarCaptchaBloqueante())
                    {
                        Log("  [CAPTCHA] Refrescando y reintentando navegacion...");
                        NavegarRecibidos();
                        Thread.Sleep(500);
                    }

                    tipoIndex++;
                    Log($"\n=== [RECIBIDOS] ({tipoIndex}/{tipos.Count}) {tipo.Value} ===");

                    if (tipoAnterior == tipo.Value)
                    {
                        Log("  ERROR: Mismo tipo, saltando...");
                        continue;
                    }
                    tipoAnterior = tipo.Value;

                    SeleccionarTipoRecibidos(tipo.Key);
                    Thread.Sleep(10);

                    for (int i = 0; i < 3; i++)
                    {
                        CerrarDialogosPrimeraVez();
                        if (!CerrarModalCaptcha()) break;
                        Thread.Sleep(5);
                    }

                    ClickConsultarRecibidos();
                    Thread.Sleep(10);
                    EsperarFinAjax(5);

                    for (int i = 0; i < 3; i++)
                    {
                        CerrarDialogosPrimeraVez();
                        if (!CerrarModalCaptcha()) break;
                        ClickConsultarRecibidos();
                        Thread.Sleep(15);
                        EsperarFinAjax(5);
                    }

                    CerrarDialogosPrimeraVez();
                    EsperarFinAjax();

                    try
                    {
                        var filasDespues = _driver!.FindElements(By.XPath("//table//tr[.//a[img]]")).Count;
                        Log($"  Filas: {filasDespues}");

                        if (filasDespues == 0)
                        {
                            Log("  Sin docs");
                            continue;
                        }

                        var (registros, descargados) = DescargarTodosRecibidos();
                        Log($"  {tipo.Value}: {descargados}/{registros}");
                        switch (tipo.Key)
                        {
                            case "01": Summary.FacturasRecibidas += descargados; break;
                            case "04": Summary.NCRecibidas += descargados; break;
                            case "05": Summary.NDRecibidas += descargados; break;
                            case "07": Summary.RetRecibidas += descargados; break;
                        }
                    }
                    catch (StaleElementReferenceException)
                    {
                        Log("  Elemento stale, reintentando...");
                        Thread.Sleep(10);
                        try
                        {
                            var filasDespues = _driver!.FindElements(By.XPath("//table//tr[.//a[img]]")).Count;
                            if (filasDespues > 0)
                            {
                                var (registros, descargados) = DescargarTodosRecibidos();
                                Log($"  {tipo.Value}: {descargados}/{registros}");
                            }
                            else
                            {
                                Log("  Sin docs");
                            }
                        }
                        catch
                        {
                            Log("  Sin docs");
                        }
                    }
                }

                EsperarDescargasFinal();
                MoverYRenombrarArchivos(dirMes, true);
            }
            catch (Exception ex)
            {
                Log($"Error recibidos: {ex.Message}");
            }
        }

        private (int totalRegistros, int docsDescargados) DescargarTodosRecibidos()
        {
            int totalDescargados = 0;
            string ultimaFirma = "";
            int paginaSinCambiar = 0;

            while (true)
            {
                var filas = _driver!.FindElements(By.XPath("//table//tr[.//a[img]]"));

                if (filas.Count == 0)
                {
                    Log("  Sin filas");
                    break;
                }

                var firmaActual = filas[0].Text.Replace(" ", "").Replace("\n", "").Trim();
                
                if (firmaActual == ultimaFirma)
                {
                    paginaSinCambiar++;
                    if (paginaSinCambiar >= 2)
                    {
                        Log("  Fin paginas");
                        break;
                    }
                }
                else
                {
                    paginaSinCambiar = 0;
                    ultimaFirma = firmaActual;
                }

                Log($"  {filas.Count} filas");

                foreach (var fila in filas)
                {
                    try
                    {
                        var btnsXml = fila.FindElements(By.XPath(".//a[img[contains(@src,'xml') or contains(@title,'XML')]]"));
                        var btnsPdf = fila.FindElements(By.XPath(".//a[img[contains(@src,'pdf')]]"));

                        if (btnsXml.Count > 0)
                        {
                            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btnsXml[0]);
                            EsperarArchivo(50);
                        }

                        if (btnsPdf.Count > 0)
                        {
                            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btnsPdf[0]);
                            EsperarArchivo(60);
                        }

                        totalDescargados++;
                    }
                    catch { }
                }

                Thread.Sleep(6);
                CerrarDialogosPrimeraVez();
                CerrarModalCaptcha();

                try
                {
                    var nxt = _driver.FindElement(By.CssSelector("span.ui-paginator-next"));
                    var clase = nxt.GetAttribute("class") ?? "";
                    if (clase.Contains("ui-state-disabled"))
                    {
                        Log("  Fin paginas");
                        break;
                    }

                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", nxt);
                    Thread.Sleep(6);
                    EsperarFinAjax();
                }
                catch { break; }
            }

            var registrosTotales = ContarRegistrosTotales();
            return (registrosTotales, totalDescargados);
        }

        private int ContarRegistrosTotales()
        {
            try
            {
                var texto = _driver!.FindElement(By.CssSelector("span.ui-paginator-current")).Text;
                var match = System.Text.RegularExpressions.Regex.Match(texto, @"de\s+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int total))
                    return total;
            }
            catch { }
            return 0;
        }

        private void EsperarArchivo(int timeoutSegundos = 10)
        {
            var end = DateTime.Now.AddSeconds(timeoutSegundos);
            while (DateTime.Now < end)
            {
                try
                {
                    var cr = Directory.GetFiles(_downloadDir, "*.crdownload");
                    if (cr.Length == 0)
                    {
                        Thread.Sleep(5);
                        return;
                    }
                }
                catch { }
                Thread.Sleep(5);
            }
        }

        private void EsperarDescargasFinal(int timeoutSegundos = 5)
        {
            var end = DateTime.Now.AddSeconds(timeoutSegundos);
            while (DateTime.Now < end)
            {
                try
                {
                    var cr = Directory.GetFiles(_downloadDir, "*.crdownload");
                    if (cr.Length == 0)
                    {
                        Thread.Sleep(10);
                        return;
                    }
                }
                catch { }
                Thread.Sleep(10);
            }
        }

        private void MoverYRenombrarArchivos(string dirDestino, bool esRecibidos)
        {
            _esRecibidos = esRecibidos;
            Thread.Sleep(5);
            var tipo = esRecibidos ? "RECIBIDOS" : "EMITIDOS";
            Log($"Moviendo y renombrando archivos ({tipo})...");

            var archivosXml = Directory.GetFiles(_downloadDir, "*.xml").OrderBy(f => File.GetCreationTime(f)).ToList();
            var archivosPdf = Directory.GetFiles(_downloadDir, "*.pdf").OrderBy(f => File.GetCreationTime(f)).ToList();
            
            Log($"XMLs: {archivosXml.Count}, PDFs: {archivosPdf.Count}");

            var nombresBase = new List<string>();

            foreach (var xmlFile in archivosXml)
            {
                try
                {
                    var datos = ExtraerDatosXml(xmlFile);
                    if (datos != null && datos.ContainsKey("clave"))
                    {
                        var nombreBase = GenerarNombreBaseDesdeClave(datos);
                        // Para EMITIDOS retenciones, usar RETENCION_VENTA
                        if (!esRecibidos && nombreBase.StartsWith("RETENCION-"))
                            nombreBase = "RETENCION_VENTA" + nombreBase.Substring(9);
                        nombresBase.Add(nombreBase);
                        
                        var destFile = Path.Combine(dirDestino, nombreBase + ".xml");
                        destFile = NombreUnico(destFile);
                        File.Move(xmlFile, destFile);
                        Log($"  XML: {Path.GetFileName(destFile)}");
                    }
                }
                catch { }
            }

            int pdfIndex = 0;
            for (int i = 0; i < nombresBase.Count && pdfIndex < archivosPdf.Count; i++)
            {
                try
                {
                    var nombreBase = nombresBase[i];
                    var pdfFile = archivosPdf[pdfIndex];
                    
                    var destFile = Path.Combine(dirDestino, nombreBase + ".pdf");
                    destFile = NombreUnico(destFile);
                    File.Move(pdfFile, destFile);
                    Log($"  PDF: {Path.GetFileName(destFile)}");
                    pdfIndex++;
                }
                catch { }
            }

            while (pdfIndex < archivosPdf.Count)
            {
                try
                {
                    var destFile = Path.Combine(dirDestino, Path.GetFileName(archivosPdf[pdfIndex]));
                    if (!File.Exists(destFile))
                        File.Move(archivosPdf[pdfIndex], destFile);
                    else
                        File.Delete(archivosPdf[pdfIndex]);
                    pdfIndex++;
                }
                catch { }
            }

            Log($"Archivos movidos a {dirDestino}");
        }

        private string GenerarNombreBaseDesdeClave(Dictionary<string, string> datos)
        {
            var clave = datos.GetValueOrDefault("clave", "");
            
            if (!string.IsNullOrEmpty(clave) && clave.Length == 49 && clave.All(char.IsDigit))
            {
                var fecha = $"{clave.Substring(4, 4)}-{clave.Substring(2, 2)}-{clave.Substring(0, 2)}";
                var ruc = clave.Substring(10, 13);
                var estab = clave.Substring(24, 3);
                var ptoEmi = clave.Substring(27, 3);
                var secuencial = clave.Substring(30, 9);
                var tipo = datos.GetValueOrDefault("tipo", "DOC");

                return $"{tipo}-{estab}-{ptoEmi}-{secuencial}-{ruc}-{fecha}";
            }

            var tipoFb = datos.GetValueOrDefault("tipo", "DOC");
            var rucFb = datos.GetValueOrDefault("ruc", "0000000000000");
            var secFb = datos.GetValueOrDefault("secuencial", "000000000");
            var estFb = datos.GetValueOrDefault("establecimiento", "000-000").Replace("-", "");
            var fechaFb = datos.GetValueOrDefault("fecha", "0000-00-00");
            
            string nombreCorto = "";
            
            // Para RECIBIDOS: usar nombreComercial del emisor (el proveedor)
            // Para EMITIDOS: usar razonSocialComprador (el cliente)
            if (datos.TryGetValue("nombreComercial", out var nc) && !string.IsNullOrWhiteSpace(nc))
                nombreCorto = GetPrimeraPalabra(nc);
            else if (datos.TryGetValue("razonSocial", out var rs) && !string.IsNullOrWhiteSpace(rs))
                nombreCorto = GetPrimeraPalabra(rs);
            
            // Si no hay nombreComercial, probar razonSocialComprador (para emitidos) o razonSocialSujetoRetenido (para retenciones)
            if (string.IsNullOrEmpty(nombreCorto))
            {
                if (datos.TryGetValue("razonSocialComprador", out var rsc) && !string.IsNullOrWhiteSpace(rsc))
                    nombreCorto = GetPrimeraPalabra(rsc);
                else if (datos.TryGetValue("razonSocialSujetoRetenido", out var rsr) && !string.IsNullOrWhiteSpace(rsr))
                    nombreCorto = GetPrimeraPalabra(rsr);
            }
            
            if (string.IsNullOrEmpty(nombreCorto))
                nombreCorto = rucFb;
            
            return $"{tipoFb}-{estFb}-{secFb}-{nombreCorto}-{fechaFb}";
        }

        private string NombreUnico(string ruta)
        {
            if (!File.Exists(ruta)) return ruta;
            
            var dir = Path.GetDirectoryName(ruta) ?? "";
            var nombre = Path.GetFileNameWithoutExtension(ruta);
            var ext = Path.GetExtension(ruta);
            
            int i = 1;
            string nuevoPath;
            do
            {
                nuevoPath = Path.Combine(dir, $"{nombre}_{i}{ext}");
                i++;
            } while (File.Exists(nuevoPath));
            
            return nuevoPath;
        }

        private Dictionary<string, string>? ExtraerDatosXml(string xmlPath)
        {
            try
            {
                var content = File.ReadAllText(xmlPath);
                var datos = new Dictionary<string, string>();

                var claveMatch = System.Text.RegularExpressions.Regex.Match(content, @"<claveAcceso>([^<]+)</claveAcceso>");
                if (claveMatch.Success) datos["clave"] = claveMatch.Groups[1].Value;

                var fechaMatch = System.Text.RegularExpressions.Regex.Match(content, @"<fechaEmision>([^<]+)</fechaEmision>");
                if (fechaMatch.Success) datos["fecha"] = fechaMatch.Groups[1].Value;

                var rucMatch = System.Text.RegularExpressions.Regex.Match(content, @"<ruc>([^<]+)</ruc>");
                if (rucMatch.Success) datos["ruc"] = rucMatch.Groups[1].Value;

                var estabMatch = System.Text.RegularExpressions.Regex.Match(content, @"<estab>([^<]+)</estab>");
                if (estabMatch.Success) datos["establecimiento"] = estabMatch.Groups[1].Value;

                var secMatch = System.Text.RegularExpressions.Regex.Match(content, @"<secuencial>([^<]+)</secuencial>");
                if (secMatch.Success) datos["secuencial"] = secMatch.Groups[1].Value;

                var codDocMatch = System.Text.RegularExpressions.Regex.Match(content, @"<codDoc>([^<]+)</codDoc>");
                if (codDocMatch.Success)
                {
                    datos["tipo"] = codDocMatch.Groups[1].Value switch
                    {
                        "01" => "FACTURA",
                        "04" => "NOTA_CREDITO",
                        "05" => "NOTA_DEBITO",
                        "07" => "RETENCION",
                        _ => codDocMatch.Groups[1].Value
                    };
                }

                var nombreComercialMatch = System.Text.RegularExpressions.Regex.Match(content, @"<nombreComercial>([^<]+)</nombreComercial>");
                if (nombreComercialMatch.Success) datos["nombreComercial"] = nombreComercialMatch.Groups[1].Value;

                var razonSocialMatch = System.Text.RegularExpressions.Regex.Match(content, @"<razonSocial>([^<]+)</razonSocial>");
                if (razonSocialMatch.Success) datos["razonSocial"] = razonSocialMatch.Groups[1].Value;

                var razonSocialCompradorMatch = System.Text.RegularExpressions.Regex.Match(content, @"<razonSocialComprador>([^<]+)</razonSocialComprador>");
                if (razonSocialCompradorMatch.Success) datos["razonSocialComprador"] = razonSocialCompradorMatch.Groups[1].Value;

                var razonSocialRetMatch = System.Text.RegularExpressions.Regex.Match(content, @"<razonSocialSujetoRetenido>([^<]+)</razonSocialSujetoRetenido>");
                if (razonSocialRetMatch.Success) datos["razonSocialSujetoRetenido"] = razonSocialRetMatch.Groups[1].Value;

                if (!datos.ContainsKey("clave") || string.IsNullOrEmpty(datos["clave"]))
                    return null;

                return datos;
            }
            catch { return null; }
        }

        private void DescargarEmitidos(SriDownloadOptions options, string dirMes)
        {
            Directory.CreateDirectory(dirMes);
            Log("=== EMITIDOS ===");
            ResetCaptchaCount();

            try
            {
                if (!NavegarEmitidos())
                {
                    Log("No se pudo navegar a Emitidos");
                    return;
                }

                Thread.Sleep(2000);

                var dias = GetDiasRango(options);
                var tipos = ObtenerTiposSeleccionados(options);

                Log($"Dias: {dias.Count}, Tipos: {tipos.Count}");

                int totalDocumentos = 0;

                SwitchToEmitidosIframe();

                foreach (var dia in dias)
                {
                    if (VerificarCaptchaBloqueante())
                    {
                        Log("  [CAPTCHA] Refrescando para Emitidos...");
                        NavegarEmitidos();
                        Thread.Sleep(2000);
                        SwitchToEmitidosIframe();
                        Thread.Sleep(500);
                    }

                    var fechaStr = $"{dia:00}/{options.Mes:00}/{options.Anio}";
                    Log($"Fecha: {fechaStr}");

                    foreach (var tipo in tipos)
                    {
                        if (!SeleccionarTipoEmitidosJS(tipo.Key))
                        {
                            Log($"  Fallo tipo: {tipo.Key}");
                            continue;
                        }

                        if (!AplicarFechaYConsultar(fechaStr))
                        {
                            Log($"  Fallo fecha");
                            continue;
                        }

                        Thread.Sleep(1000);
                        CerrarDialogosPrimeraVez();
                        CerrarModalCaptcha();
                        EsperarFinAjax(5);

                        try
                        {
                            var filas = _driver.FindElements(By.XPath("//tbody[@id='frmPrincipal:tablaCompEmitidos_data']/tr"));
                            if (filas.Count == 0)
                            {
                                Log($"  {tipo.Value}: sin documentos");
                                continue;
                            }

                            Log($"  {tipo.Value}: {filas.Count} docs");

                            for (int i = 0; i < filas.Count; i++)
                            {
                                try
                                {
                                    var filaIdx = i + 1;
                                    CerrarPopup();
                                    Thread.Sleep(10);

                                    var snap = SnapshotArchivos(new[] { ".pdf", ".crdownload" });

                                    var descargoPdf = DescargarSoloPdf(filaIdx);

                                    if (!descargoPdf)
                                    {
                                        Log($"    Fila {filaIdx}: PDF no descargado");
                                    }
                                    else
                                    {
                                        Log($"    Fila {filaIdx}: OK");
                                        totalDocumentos++;
                                        switch (tipo.Key.ToLower())
                                        {
                                            case "factura": Summary.FacturasEmitidas++; break;
                                            case "notas de credito": Summary.NCEmitidas++; break;
                                            case "notas de debito": Summary.NDEmitidas++; break;
                                            case "retencion": Summary.RetEmitidas++; break;
                                        }
                                    }

                                    Thread.Sleep(15);
                                }
                                catch (Exception ex)
                                {
                                    Log($"    Error {i + 1}: {ex.Message}");
                                    CerrarPopup();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"  Error al leer filas: {ex.Message}");
                        }

                        Thread.Sleep(20);
                    }
                }

                Log($"Total PDFs Emitidos: {totalDocumentos}");
                MoverYRenombrarArchivos(dirMes, false);
                Log("=== COMPLETADO ===");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
            }
        }

        private class PdfDatosResult
        {
            public Dictionary<string, string> datos { get; set; } = new();
        }

        private PdfDatosResult? DescargarPdfYObtenerDatos(int filaNum)
        {
            try
            {
                var result = new PdfDatosResult();
                var filas = _driver.FindElements(By.XPath("//tbody[@id='frmPrincipal:tablaCompEmitidos_data']/tr"));
                if (filaNum > filas.Count) return null;

                var fila = filas[filaNum - 1];
                var tds = fila.FindElements(By.TagName("td"));
                if (tds.Count < 3) return null;

                var celdaPdf = tds[tds.Count - 1];
                var pdfLink = celdaPdf.FindElements(By.XPath(".//img[contains(@src,'pdf')]/parent::a"));
                
                if (pdfLink.Count > 0)
                {
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", pdfLink[0]);
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", pdfLink[0]);
                }

                Thread.Sleep(50);

                var celdaClave = tds[2];
                var links = celdaClave.FindElements(By.TagName("a"));
                IWebElement claveLink = links.Count > 0 ? links[0] : celdaClave;
                
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", claveLink);
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", claveLink);
                Thread.Sleep(100);

                var pageSource = _driver.PageSource;
                var hasPopup = pageSource.Contains("Detalle del Comprobante") || pageSource.Contains("clave de acceso");

                if (!hasPopup)
                {
                    CerrarPopup();
                    return null;
                }

                result.datos["claveAcceso"] = Extract(pageSource, @"<label>Clave de acceso\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["fechaEmision"] = Extract(pageSource, @"<label>Fecha Emisión\s*</label></td>\s*<td[^>]*><label>([\d/]+)</label>");
                result.datos["razonSocial"] = Extract(pageSource, @"<label>Razón Social\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["ruc"] = Extract(pageSource, @"<label>Número RUC\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                result.datos["estab"] = Extract(pageSource, @"<label>Establecimiento\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                result.datos["ptoEmi"] = Extract(pageSource, @"<label>Punto de emisión\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                result.datos["secuencial"] = Extract(pageSource, @"<label>Secuencial\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["totalSinImpuestos"] = Extract(pageSource, @"<label>Total Sin impuestos\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                result.datos["importeTotal"] = Extract(pageSource, @"<label>Importe Total\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                result.datos["ambiente"] = Extract(pageSource, @"<label>Ambiente\s*</label></td>\s*<td[^>]*><label>(\d)</label>");
                result.datos["tipoEmision"] = Extract(pageSource, @"<label>Tipo de emisión\s*</label></td>\s*<td[^>]*><label>(\d)</label>");
                result.datos["codDocModificado"] = Extract(pageSource, @"<label>Cod Doc Modificado\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                result.datos["numDocModificado"] = Extract(pageSource, @"<label>Num Doc Modificado\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["valorModificacion"] = Extract(pageSource, @"<label>Valor Modificacion\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                result.datos["motivo"] = Extract(pageSource, @"<label>Motivo\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["periodoFiscal"] = Extract(pageSource, @"<label>Periodo Fiscal\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["razonSocialComprador"] = Extract(pageSource, @"<label>Razón Social Comprador\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["identificacionComprador"] = Extract(pageSource, @"<label>Identificación Comprador\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                result.datos["tipoIdentificacionComprador"] = Extract(pageSource, @"<label>Tipo Identificación Comprador\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");

                result.datos["razonSocialSujetoRetenido"] = Extract(pageSource, @"<label>Razón Social(?: de)? Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                result.datos["identificacionSujetoRetenido"] = Extract(pageSource, @"<label>Identificación Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                result.datos["tipoIdentificacionSujetoRetenido"] = Extract(pageSource, @"<label>Tipo Identificación Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");

                if (!string.IsNullOrEmpty(result.datos["claveAcceso"]) && result.datos["claveAcceso"].Length >= 10)
                    result.datos["tipoDoc"] = result.datos["claveAcceso"].Substring(8, 2);

                if (result.datos.GetValueOrDefault("tipoDoc") == "07")
                {
                    if (string.IsNullOrEmpty(result.datos.GetValueOrDefault("importeTotal", "")))
                        result.datos["importeTotal"] = Extract(pageSource, @"<label>Total\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                    if (string.IsNullOrEmpty(result.datos.GetValueOrDefault("totalSinImpuestos", "")))
                        result.datos["totalSinImpuestos"] = Extract(pageSource, @"<label>Subtotal\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                    ExtraerRetencionesDesdeLabels(pageSource, result.datos);
                }

                CerrarPopup();

                if (string.IsNullOrEmpty(result.datos["claveAcceso"]) || result.datos["claveAcceso"].Length < 49)
                    return null;

                return result;
            }
            catch (Exception ex)
            {
                CerrarPopup();
                return null;
            }
        }

        private bool DescargarSoloPdf(int filaNum)
        {
            try
            {
                var filaXPath = $"//tbody[@id='frmPrincipal:tablaCompEmitidos_data']/tr[{filaNum}]";
                var filas = _driver.FindElements(By.XPath(filaXPath));
                if (filas.Count == 0) return false;
                
                var fila = filas[0];
                var tds = fila.FindElements(By.TagName("td"));
                
                // Obtener clave de la tabla directamente (NO del popup) para saber el tipo
                string claveTabla = "";
                string fechaIso = "";
                
                if (tds.Count >= 5)
                {
                    claveTabla = new string(tds[2].Text.Where(char.IsDigit).ToArray());
                    var fechaRaw = tds[4].Text.Trim();
                    if (DateTime.TryParseExact(fechaRaw, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var fecha))
                        fechaIso = fecha.ToString("yyyy-MM-dd");
                }
                
                // Determinar tipo de documento desde la tabla
                string tipoDoc = "DOC";
                string tipoNum = "00";
                if (claveTabla.Length >= 49)
                {
                    tipoNum = claveTabla.Substring(8, 2);
                    tipoDoc = tipoNum switch
                    {
                        "01" => "FACTURA",
                        "04" => "NOTA_CREDITO",
                        "05" => "NOTA_DEBITO",
                        "07" => "RETENCION_VENTA",
                        _ => "DOC"
                    };
                }
                
                Log($"    DEBUG Fila {filaNum}: tipoNum={tipoNum}, claveTabla={claveTabla.Substring(0, Math.Min(20, claveTabla.Length))}...");
                
                // Extraer datos del popup (clave de acceso)
                var datos = ExtraerDatosPopup(fila, tds);
                if (datos != null && datos.ContainsKey("claveAcceso") && !string.IsNullOrEmpty(datos["claveAcceso"]))
                {
                    // Verificar que la clave del popup coincida con la de la tabla
                    var clavePopup = datos["claveAcceso"];
                    var tipoNumPopup = clavePopup.Length >= 10 ? clavePopup.Substring(8, 2) : "00";
                    
                    Log($"    DEBUG Popup tipo={tipoNumPopup}, clave={clavePopup.Substring(0, Math.Min(15, clavePopup.Length))}...");
                    
                    // Si el tipo del popup no coincide, usar el de la tabla
                    if (tipoNumPopup != tipoNum)
                    {
                        Log($"    DEBUG Corrigiendo tipoDoc: popup={tipoNumPopup}, tabla={tipoNum}");
                        datos["tipoDoc"] = tipoNum;
                    }
                    
                    Log($"    Fila {filaNum}: {tipoDoc} - datos extraidos");
                    
                    // Generar XML desde los datos
                    GenerarXmlEmitido(datos);
                }
                else
                {
                    Log($"    Fila {filaNum}: {tipoDoc} - sin popup");
                }
                
                // Buscar PDF link
                var pdfLinkXPath = $"{filaXPath}//a[img[contains(@src,'pdf')]]";
                var pdfLinks = _driver.FindElements(By.XPath(pdfLinkXPath));
                
                if (pdfLinks.Count == 0)
                {
                    var pdfLinkXPath2 = $"{filaXPath}//img[contains(@src,'pdf')]/parent::a";
                    pdfLinks = _driver.FindElements(By.XPath(pdfLinkXPath2));
                }
                
                if (pdfLinks.Count == 0)
                {
                    Log($"    Fila {filaNum}: sin PDF link");
                    return false;
                }

                var pdfLink = pdfLinks[0];
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", pdfLink);
                Thread.Sleep(50);
                
                // Snapshot para PDF
                var snap = SnapshotArchivos(new[] { ".pdf", ".crdownload" });
                var windowCount = _driver.WindowHandles.Count;
                
                // Click PDF
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", pdfLink);
                Thread.Sleep(300);
                
                if (_driver.WindowHandles.Count > windowCount)
                {
                    try
                    {
                        var newWindow = _driver.WindowHandles[_driver.WindowHandles.Count - 1];
                        _driver.SwitchTo().Window(newWindow);
                        _driver.Close();
                        _driver.SwitchTo().Window(_driver.WindowHandles[0]);
                    }
                    catch { }
                }
                
                var nuevoPdf = EsperarArchivoNuevo(".pdf", snap, 30);
                if (nuevoPdf != null)
                {
                    var claveParaRenombrar = datos?.ContainsKey("claveAcceso") == true ? datos["claveAcceso"] : claveTabla;
                    RenombrarArchivoEmitido(nuevoPdf, tipoDoc, claveParaRenombrar, fechaIso);
                    Log($"    Fila {filaNum}: PDF OK");
                }
                else
                {
                    Log($"    Fila {filaNum}: timeout PDF");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log($"    Fila {filaNum}: err {ex.Message}");
                return false;
            }
        }
        
        private Dictionary<string, string>? ExtraerDatosPopup(IWebElement fila, ReadOnlyCollection<IWebElement> tds)
        {
            try
            {
                var datos = new Dictionary<string, string>();
                
                // Cerrar popup existente antes de abrir uno nuevo
                CerrarPopup();
                Thread.Sleep(200);
                
                // Click en la clave de acceso para abrir popup
                if (tds.Count >= 3)
                {
                    var celdaClave = tds[2];
                    var links = celdaClave.FindElements(By.TagName("a"));
                    
                    if (links.Count > 0)
                    {
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", links[0]);
                        Thread.Sleep(50);
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", links[0]);
                    }
                    else
                    {
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", celdaClave);
                        Thread.Sleep(50);
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", celdaClave);
                    }
                }
                
                // Esperar popup con verificacion
                Thread.Sleep(800);
                
                // Esperar a que el popup tenga contenido (clave de acceso)
                var maxWait = 5;
                var pageSource = _driver.PageSource;
                while (!pageSource.Contains("Clave de acceso") && maxWait > 0)
                {
                    Thread.Sleep(200);
                    pageSource = _driver.PageSource;
                    maxWait--;
                }
                
                // Extraer datos via PageSource
                Log($"    DEBUG Popup: pageSource length = {pageSource.Length}, tiene Clave={pageSource.Contains("Clave de acceso")}");
                
                // Debug: guardar page source para analisis
                Log($"    DEBUG Popup: pageSource length = {pageSource.Length}");
                
                datos["claveAcceso"] = Extract(pageSource, @"<label>Clave de acceso\s*</label></td>\s*<td[^>]*><label>(\d{49})</label>");
                var claveLen = datos["claveAcceso"].Length;
                Log($"    DEBUG Clave extraida: len={claveLen}, '{(claveLen > 0 ? datos["claveAcceso"].Substring(0, Math.Min(25, claveLen)) + "..." : "VACIA")}'");
                
                datos["fechaEmision"] = Extract(pageSource, @"<label>Fecha Emisión\s*</label></td>\s*<td[^>]*><label>([\d/]+)</label>");
                datos["razonSocial"] = Extract(pageSource, @"<label>Razón Social\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["ruc"] = Extract(pageSource, @"<label>Número RUC\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["estab"] = Extract(pageSource, @"<label>Establecimiento\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["ptoEmi"] = Extract(pageSource, @"<label>Punto de emisión\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["secuencial"] = Extract(pageSource, @"<label>Secuencial\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["razonSocialComprador"] = Extract(pageSource, @"<label>Razón Social Comprador\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["identificacionComprador"] = Extract(pageSource, @"<label>Identificación Comprador\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["tipoIdentificacionComprador"] = Extract(pageSource, @"<label>Tipo Identificación Comprador\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");

                datos["razonSocialSujetoRetenido"] = Extract(pageSource, @"<label>Razón Social(?: de)? Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["identificacionSujetoRetenido"] = Extract(pageSource, @"<label>Identificación Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["tipoIdentificacionSujetoRetenido"] = Extract(pageSource, @"<label>Tipo Identificación Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");

                datos["totalSinImpuestos"] = Extract(pageSource, @"<label>Total Sin impuestos\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                datos["importeTotal"] = Extract(pageSource, @"<label>Importe Total\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                datos["ambiente"] = Extract(pageSource, @"<label>Ambiente\s*</label></td>\s*<td[^>]*><label>(\d)</label>");
                datos["tipoEmision"] = Extract(pageSource, @"<label>Tipo de emisión\s*</label></td>\s*<td[^>]*><label>(\d)</label>");
                
                // Extraer tipo de documento de la clave de acceso
                if (!string.IsNullOrEmpty(datos["claveAcceso"]) && datos["claveAcceso"].Length >= 10)
                {
                    datos["tipoDoc"] = datos["claveAcceso"].Substring(8, 2);
                    Log($"    DEBUG tipoDoc from clave: {datos["tipoDoc"]}");
                }
                
                // DEBUG: dump all labels for retention documents
                if (datos.GetValueOrDefault("tipoDoc") == "07")
                {
                    var labelMatches = Regex.Matches(pageSource, @"<label>([^<]*)</label>", RegexOptions.IgnoreCase);
                    Log($"    DEBUG RETENCION: {labelMatches.Count} labels en popup");
                    foreach (Match lm in labelMatches)
                    {
                        var txt = lm.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(txt)) Log($"      Label: '{txt}'");
                    }
                }
                
                // Campos adicionales para NC y ND
                datos["codDocModificado"] = Extract(pageSource, @"<label>Cod Doc Modificado\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["numDocModificado"] = Extract(pageSource, @"<label>Num Doc Modificado\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["fechaEmisionDocSustento"] = Extract(pageSource, @"<label>Fecha Emision Doc Sustento\s*</label></td>\s*<td[^>]*><label>([\d/]+)</label>");
                datos["valorModificacion"] = Extract(pageSource, @"<label>Valor Modificacion\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                datos["motivo"] = Extract(pageSource, @"<label>Motivo\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["periodoFiscal"] = Extract(pageSource, @"<label>Periodo Fiscal\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                
                // Para Retenciones: extraer número del documento afectado
                datos["numDocSustento"] = Extract(pageSource, @"<label>Doc\.?\s*Sustento\s*\(Factura\)\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                if (string.IsNullOrEmpty(datos["numDocSustento"]))
                    datos["numDocSustento"] = Extract(pageSource, @"<label>Num\.?\s*Doc\.?\s*Sustento\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                if (string.IsNullOrEmpty(datos["numDocSustento"]))
                    datos["numDocSustento"] = Extract(pageSource, @"<label>No\.?\s*Documento\s*Sustento\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                
                // Agente Retencion
                datos["agenteRetencion"] = Extract(pageSource, @"<label>Agente Retención\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                
                // Campos para Retenciones
                datos["contribuyenteEspecial"] = Extract(pageSource, @"<label>Contribuyente Especial\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                
                // Extraer impuestos desde la tabla de totales (para NC, ND y Facturas)
                ExtraerImpuestos(pageSource, datos);
                
                // Extraer detalle de retenciones si existe
                ExtraerRetenciones(pageSource, datos);
                ExtraerRetencionesDesdeLabels(pageSource, datos);
                
                // Fallback para retenciones: "Total" en lugar de "Importe Total"
                if (datos.GetValueOrDefault("tipoDoc") == "07")
                {
                    if (string.IsNullOrEmpty(datos.GetValueOrDefault("importeTotal", "")))
                        datos["importeTotal"] = Extract(pageSource, @"<label>Total\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                    if (string.IsNullOrEmpty(datos.GetValueOrDefault("totalSinImpuestos", "")))
                        datos["totalSinImpuestos"] = Extract(pageSource, @"<label>Subtotal\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                    
                    // Convertir fechaDocSustento de formato SRI (yyyy-MM-dd HH:mm:ss.f) a dd/MM/yyyy
                    var fechaDocSust = datos.GetValueOrDefault("fechaEmisionDocSustento", "");
                    if (!string.IsNullOrEmpty(fechaDocSust) && fechaDocSust.Contains("-") && fechaDocSust.Length >= 10)
                    {
                        var partes = fechaDocSust.Split(' ');
                        if (partes.Length > 0)
                        {
                            var fechaParts = partes[0].Split('-');
                            if (fechaParts.Length == 3)
                                datos["fechaEmisionDocSustento"] = $"{fechaParts[2]}/{fechaParts[1]}/{fechaParts[0]}";
                        }
                    }
                }
                
                // Cerrar popup
                CerrarPopup();
                
                return datos;
            }
            catch (Exception ex)
            {
                Log($"    Error popup: {ex.Message}");
                CerrarPopup();
                return null;
            }
        }
        
        private void ExtraerImpuestos(string pageSource, Dictionary<string, string> datos)
        {
            // Buscar tabla de totales por impuesto - varias ids posibles
            var totalesMatch = Regex.Match(pageSource, 
                @"id=""[^""]*tabla-totales[^""]*""[^>]*>.*?<tbody[^>]*>(.*?)</tbody>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            if (!totalesMatch.Success) {
                Log("    ExtraerImpuestos: No se encontró tabla de totales");
            }
            else
            {
                var tbodyContent = totalesMatch.Groups[1].Value;
                
                // Extraer todas las filas de la tabla - formato más flexible
                var filas = Regex.Matches(tbodyContent, 
                    @"<td[^>]*>(\d+)</td>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>([\d.]*)</td>\s*<td[^>]*>([\d.]+)</td>\s*<td[^>]*>([\d.]+)</td>",
                    RegexOptions.Singleline);
                
                int impuestoIndex = 0;
                foreach (Match fila in filas)
                {
                    var codigo = fila.Groups[1].Value;
                    var nombre = fila.Groups[2].Value.Trim();
                    var tarifa = fila.Groups[3].Value;
                    var baseImponible = fila.Groups[4].Value;
                    var valor = fila.Groups[5].Value;
                    
                    // Guardar todo, no filtrar
                    datos[$"impuesto{impuestoIndex}_codigo"] = codigo;
                    datos[$"impuesto{impuestoIndex}_nombre"] = nombre;
                    datos[$"impuesto{impuestoIndex}_tarifa"] = tarifa;
                    datos[$"impuesto{impuestoIndex}_base"] = baseImponible;
                    datos[$"impuesto{impuestoIndex}_valor"] = valor;
                    Log($"    Impuesto {impuestoIndex}: cod={codigo}, nombre={nombre}, tarifa={tarifa}, base={baseImponible}, valor={valor}");
                    impuestoIndex++;
                }
                
                datos["impuestoCount"] = impuestoIndex.ToString();
                Log($"    Total impuestos extraídos de tabla: {impuestoIndex}");
            }
            
            // SI NO HAY VALORES, buscar en tabla de detalles (donde está el IVA del ejemplo: 72.59)
            if (datos.GetValueOrDefault("impuestoCount", "0") == "0" || datos.GetValueOrDefault("impuesto0_valor", "0") == "0")
            {
                // Buscar en tabla de detalles: form-detalle-factura:tabla-detalles-factura:X:tabla-impuestos-detalle-factura
                var detallesImpMatch = Regex.Matches(pageSource,
                    @"tabla-impuestos-detalle-factura.*?<td[^>]*>(\d+)</td>.*?<td[^>]*>([\d.]*)</td>.*?<td[^>]*>([\d.]+)</td>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                
                Log($"    Buscando en tabla de detalles: {detallesImpMatch.Count} filas encontradas");
                
                int impDetIndex = 0;
                foreach (Match m in detallesImpMatch)
                {
                    var codImp = m.Groups[1].Value;
                    var baseImp = m.Groups[2].Value;
                    var valImp = m.Groups[3].Value;
                    
                    if (!string.IsNullOrEmpty(valImp) && valImp != "0")
                    {
                        var idx = int.Parse(datos.GetValueOrDefault("impuestoCount", "0"));
                        datos[$"impuesto{idx}_codigo"] = codImp;
                        datos[$"impuesto{idx}_nombre"] = codImp == "2" ? "IVA" : "OTRO";
                        datos[$"impuesto{idx}_tarifa"] = "15";
                        datos[$"impuesto{idx}_base"] = baseImp;
                        datos[$"impuesto{idx}_valor"] = valImp;
                        Log($"    Impuesto detalle {idx}: cod={codImp}, base={baseImp}, valor={valImp}");
                        impDetIndex++;
                    }
                }
                
                if (impDetIndex > 0)
                {
                    var nuevoCount = int.Parse(datos.GetValueOrDefault("impuestoCount", "0")) + impDetIndex;
                    datos["impuestoCount"] = nuevoCount.ToString();
                    Log($"    Total impuestos con detalles: {nuevoCount}");
                }
            }
            
            // Fallback: buscar directamente las etiquetas IVA en la página
            var iva15Match = Regex.Match(pageSource, @"IVA\s*15%.*?<td[^>]*>([\d.]+)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (iva15Match.Success && iva15Match.Groups[1].Value != "0" && (datos.GetValueOrDefault("impuestoCount", "0") == "0" || datos.GetValueOrDefault("impuesto0_valor", "0") == "0"))
            {
                var totalSinIva = Regex.Match(pageSource, @"SUBTOTAL\s*15%.*?<td[^>]*>([\d.]+)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                datos["impuesto0_codigo"] = "2";
                datos["impuesto0_nombre"] = "IVA";
                datos["impuesto0_tarifa"] = "15";
                datos["impuesto0_base"] = totalSinIva.Success ? totalSinIva.Groups[1].Value : "0";
                datos["impuesto0_valor"] = iva15Match.Groups[1].Value;
                datos["impuestoCount"] = "1";
                Log($"    Fallback IVA 15%: valor={iva15Match.Groups[1].Value}, base={datos.GetValueOrDefault("impuesto0_base", "0")}");
            }
        }
        
        private void ExtraerRetenciones(string pageSource, Dictionary<string, string> datos)
        {
            // Buscar tabla de retenciones
            var retencionesMatch = Regex.Match(pageSource, 
                @"id=""[^""]*tabla[^""]*retencion(?:es)?[^""]*""[^>]*>.*?<tbody[^>]*>(.*?)</tbody>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            if (!retencionesMatch.Success)
            {
                Log("    ExtraerRetenciones: no se encontró tabla de retenciones");
                return;
            }
            
            var tbodyContent = retencionesMatch.Groups[1].Value;
            Log($"    ExtraerRetenciones: tbody length={tbodyContent.Length}");
            
            int retIndex = 0;
            
            // Intento 1: formato 7 columnas (retenciones ventas):
            // codigo | nombre | base | % | valor | numDocSustento | fechaDocSustento
            var filas7 = Regex.Matches(tbodyContent,
                @"<td[^>]*>(\d+)</td>\s*<td[^>]*>([^<]+)</td>\s*<td[^>]*>([\d.]+)</td>\s*<td[^>]*>([\d.]+)</td>\s*<td[^>]*>([\d.]+)</td>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>([^<]*)</td>",
                RegexOptions.Singleline);
            
            if (filas7.Count > 0)
            {
                foreach (Match fila in filas7)
                {
                    var codigo = fila.Groups[1].Value;
                    var nombre = fila.Groups[2].Value.Trim();
                    var baseImponible = fila.Groups[3].Value;
                    var porcentaje = fila.Groups[4].Value;
                    var valor = fila.Groups[5].Value;
                    var numDocSust = fila.Groups[6].Value.Trim();
                    var fechaDocSust = fila.Groups[7].Value.Trim();
                    
                    datos[$"retencion{retIndex}_codigo"] = codigo;
                    datos[$"retencion{retIndex}_base"] = baseImponible;
                    datos[$"retencion{retIndex}_porcentaje"] = porcentaje;
                    datos[$"retencion{retIndex}_valor"] = valor;
                    datos[$"retencion{retIndex}_nombre"] = nombre;
                    datos["numDocSustento"] = numDocSust;
                    datos["fechaEmisionDocSustento"] = fechaDocSust;
                    Log($"    Retencion {retIndex}: cod={codigo}, nombre={nombre}, base={baseImponible}, %={porcentaje}, valor={valor}, doc={numDocSust}, fecha={fechaDocSust}");
                    retIndex++;
                }
            }
            else
            {
                // Intento 2: formato 4 columnas clasico:
                // codigo | base | % | valor
                var filas4 = Regex.Matches(tbodyContent,
                    @"<td[^>]*>(\d+)</td>\s*<td[^>]*>([\d.]+)</td>\s*<td[^>]*>([\d.]+)</td>\s*<td[^>]*>([\d.]+)</td>",
                    RegexOptions.Singleline);
                
                foreach (Match fila in filas4)
                {
                    var codigo = fila.Groups[1].Value;
                    var baseImponible = fila.Groups[2].Value;
                    var porcentaje = fila.Groups[3].Value;
                    var valor = fila.Groups[4].Value;
                    
                    datos[$"retencion{retIndex}_codigo"] = codigo;
                    datos[$"retencion{retIndex}_base"] = baseImponible;
                    datos[$"retencion{retIndex}_porcentaje"] = porcentaje;
                    datos[$"retencion{retIndex}_valor"] = valor;
                    Log($"    Retencion {retIndex} (4 col): cod={codigo}, base={baseImponible}, %={porcentaje}, valor={valor}");
                    retIndex++;
                }
            }
            
            datos["retencionCount"] = retIndex.ToString();
            Log($"    Total retenciones extraídas: {retIndex}");
        }
        
        private void ExtraerRetencionesDesdeLabels(string pageSource, Dictionary<string, string> datos)
        {
            if (datos.GetValueOrDefault("retencionCount", "0") != "0") return;
            
            // Para retenciones, los datos pueden estar en campos individuales del popup
            var baseRetencion = Extract(pageSource, @"<label>Base Imponible\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
            var porcentajeRet = Extract(pageSource, @"<label>(?:Porcentaje\s*)?Retención\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
            var valorRetenido = Extract(pageSource, @"<label>Valor Retenido\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
            var codRetencion = Extract(pageSource, @"<label>Código\s*de\s*Retención\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
            var codigoImpuesto = Extract(pageSource, @"<label>Código\s*de\s*Impuesto\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
            
            if (string.IsNullOrEmpty(baseRetencion) && string.IsNullOrEmpty(porcentajeRet) && string.IsNullOrEmpty(valorRetenido))
            {
                // Intentar patrón alternativo desde tabla de detalle de retencion
                var retDetalle = Regex.Match(pageSource, 
                    @"Código[^<]*</label>[^<]*<label[^>]*>(\d+)</label>.*?Base[^<]*</label>[^<]*<label[^>]*>([\d.]+)</label>.*?Porcentaje[^<]*</label>[^<]*<label[^>]*>([\d.]+)</label>.*?Valor[^<]*</label>[^<]*<label[^>]*>([\d.]+)</label>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (retDetalle.Success)
                {
                    codigoImpuesto = retDetalle.Groups[1].Value;
                    baseRetencion = retDetalle.Groups[2].Value;
                    porcentajeRet = retDetalle.Groups[3].Value;
                    valorRetenido = retDetalle.Groups[4].Value;
                }
            }
            
            if (!string.IsNullOrEmpty(baseRetencion) || !string.IsNullOrEmpty(valorRetenido))
            {
                datos["retencion0_codigo"] = string.IsNullOrEmpty(codigoImpuesto) ? "1" : codigoImpuesto;
                datos["retencion0_base"] = string.IsNullOrEmpty(baseRetencion) ? "0.00" : baseRetencion;
                datos["retencion0_porcentaje"] = string.IsNullOrEmpty(porcentajeRet) ? (string.IsNullOrEmpty(codRetencion) ? "0" : codRetencion) : porcentajeRet;
                datos["retencion0_valor"] = string.IsNullOrEmpty(valorRetenido) ? "0.00" : valorRetenido;
                datos["retencionCount"] = "1";
                Log($"    Retencion desde labels: cod={datos["retencion0_codigo"]}, base={datos["retencion0_base"]}, %={datos["retencion0_porcentaje"]}, valor={datos["retencion0_valor"]}");
            }
        }
        
        private void GenerarXmlEmitido(Dictionary<string, string> datos)
        {
            try
            {
                var clave = datos.GetValueOrDefault("claveAcceso", "");
                var tipoDoc = datos.GetValueOrDefault("tipoDoc", "");
                
                Log($"    DEBUG XML: clave={clave.Substring(0, Math.Min(20, clave.Length))}... tipoDoc={tipoDoc}");
                
                if (string.IsNullOrEmpty(clave) || clave.Length < 49)
                {
                    Log("    XML: sin clave de acceso");
                    return;
                }
                
                // Si tipoDoc no se extrajo del popup, obtenerlo de la clave
                if (string.IsNullOrEmpty(tipoDoc) && clave.Length >= 10)
                {
                    tipoDoc = clave.Substring(8, 2);
                    datos["tipoDoc"] = tipoDoc;
                }
                
                var fecha = datos.GetValueOrDefault("fechaEmision", "01/01/2026");
                
                // Convertir fecha
                var fechaParts = fecha.Split('/');
                var fechaIso = fechaParts.Length == 3 ? $"{fechaParts[2]}-{fechaParts[1]}-{fechaParts[0]}" : fecha;
                
                string comprobanteXml;
                string tipoNombre;
                
                switch (tipoDoc)
                {
                    case "01":
                        comprobanteXml = GenerarXmlFactura(datos, clave, fecha);
                        tipoNombre = "FACTURA";
                        break;
                    case "04":
                        comprobanteXml = GenerarXmlNotaCredito(datos, clave, fecha);
                        tipoNombre = "NOTA_CREDITO";
                        break;
                    case "05":
                        comprobanteXml = GenerarXmlNotaDebito(datos, clave, fecha);
                        tipoNombre = "NOTA_DEBITO";
                        break;
                    case "07":
                        comprobanteXml = GenerarXmlRetencion(datos, clave, fecha);
                        tipoNombre = "RETENCION_VENTA";
                        break;
                    default:
                        Log($"    XML: tipo no soportado {tipoDoc}");
                        return;
                }
                
                var xmlFinal = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<autorizacion>
  <estado>AUTORIZADO</estado>
  <numeroAutorizacion>{clave}</numeroAutorizacion>
  <fechaAutorizacion>{fecha}</fechaAutorizacion>
  <comprobante><![CDATA[{comprobanteXml}]]></comprobante>
</autorizacion>";

                var estab = clave.Substring(24, 3);
                var punto = clave.Substring(27, 3);
                var secuencial = clave.Substring(30, 9);
                var ruc = clave.Substring(10, 13);
                
                var nombreXml = $"{tipoNombre}-{estab}{punto}-{secuencial}-{ruc}-{fechaIso}.xml";
                nombreXml = LimpiarNombreArchivo(nombreXml);
                
                var xmlPath = Path.Combine(_downloadDir, nombreXml);
                int i = 1;
                while (File.Exists(xmlPath))
                {
                    xmlPath = Path.Combine(_downloadDir, $"{tipoNombre}-{estab}{punto}-{secuencial}-{ruc}-{fechaIso}_{i}.xml");
                    i++;
                }
                
                File.WriteAllText(xmlPath, xmlFinal, System.Text.Encoding.UTF8);
                Log($"    XML: {Path.GetFileName(xmlPath)}");
            }
            catch (Exception ex)
            {
                Log($"    XML error: {ex.Message}");
            }
        }
        
        private void RenombrarArchivoEmitido(string filePath, string tipo, string claveAcceso, string fechaIso)
        {
            try
            {
                if (string.IsNullOrEmpty(claveAcceso) || claveAcceso.Length < 49)
                    return;
                    
                var estab = claveAcceso.Substring(24, 3);
                var punto = claveAcceso.Substring(27, 3);
                var secuencial = claveAcceso.Substring(30, 9);
                var numDoc = $"{estab}{punto}-{secuencial}";
                var ruc = claveAcceso.Substring(10, 13);
                
                var ext = Path.GetExtension(filePath);
                var nuevoNombre = $"{tipo}-{numDoc}-{ruc}-{fechaIso}{ext}";
                nuevoNombre = LimpiarNombreArchivo(nuevoNombre);
                
                var dir = Path.GetDirectoryName(filePath) ?? _downloadDir;
                var nuevoPath = Path.Combine(dir, nuevoNombre);
                
                int i = 1;
                while (File.Exists(nuevoPath))
                {
                    nuevoPath = Path.Combine(dir, $"{tipo}-{numDoc}-{ruc}-{fechaIso}_{i}{ext}");
                    i++;
                }
                
                File.Move(filePath, nuevoPath);
                Log($"    Renombrado: {Path.GetFileName(nuevoPath)}");
            }
            catch (Exception ex)
            {
                Log($"    Error renombrar: {ex.Message}");
            }
        }
        
        private string LimpiarNombreArchivo(string nombre)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
                nombre = nombre.Replace(c, '_');
            return nombre;
        }

        private bool AplicarFechaYConsultar(string fechaStr)
        {
            try
            {
                var inputFecha = _wait!.Until(ExpectedConditions.ElementToBeClickable(
                    By.CssSelector("input[id*='frmPrincipal'][id$='_input']")));

                ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].removeAttribute('readonly');", inputFecha);
                
                inputFecha.SendKeys(Keys.Control + "a");
                inputFecha.SendKeys(Keys.Backspace);
                inputFecha.SendKeys(fechaStr);
                inputFecha.SendKeys(Keys.Tab);

                Thread.Sleep(5);

                var btnConsultar = _driver.FindElement(By.XPath("//button[.//span[contains(.,'Consultar')]]"));
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btnConsultar);

                Thread.Sleep(10);

                return true;
            }
            catch (Exception ex)
            {
                Log($"  Error fecha: {ex.Message}");
                return false;
            }
        }

        private void CerrarPopup()
        {
            try
            {
                var closeBtn = _driver.FindElement(By.CssSelector(".ui-dialog-titlebar-close"));
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", closeBtn);
            }
            catch { }
        }

        private Dictionary<string, DateTime> SnapshotArchivos(string[] extensiones)
        {
            var snap = new Dictionary<string, DateTime>();
            try
            {
                foreach (var f in Directory.GetFiles(_downloadDir))
                {
                    var extLower = Path.GetExtension(f).ToLower();
                    if (extensiones.Any(e => extLower == e.ToLower()))
                    {
                        snap[f] = File.GetLastWriteTime(f);
                    }
                }
            }
            catch { }
            return snap;
        }

        private string? EsperarArchivoNuevo(string ext, Dictionary<string, DateTime> antes, int timeoutSegundos = 30)
        {
            ext = ext.ToLower();
            var end = DateTime.Now.AddSeconds(timeoutSegundos);

            while (DateTime.Now < end)
            {
                try
                {
                    var crFiles = Directory.GetFiles(_downloadDir, "*.crdownload");
                    if (crFiles.Length > 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    var candidatos = new List<(DateTime mtime, string path)>();
                    foreach (var f in Directory.GetFiles(_downloadDir))
                    {
                        if (!Path.GetExtension(f).ToLower().EndsWith(ext))
                            continue;

                        try
                        {
                            var mtime = File.GetLastWriteTime(f);
                            if (!antes.TryGetValue(f, out var oldMtime) || mtime > oldMtime)
                            {
                                candidatos.Add((mtime, f));
                            }
                        }
                        catch { }
                    }

                    if (candidatos.Count > 0)
                    {
                        candidatos.Sort((a, b) => b.mtime.CompareTo(a.mtime));
                        return candidatos[0].path;
                    }
                }
                catch { }

                Thread.Sleep(10);
            }

            return null;
        }

        private void SwitchToEmitidosIframe()
        {
            try
            {
                _driver!.SwitchTo().DefaultContent();
                Thread.Sleep(500);
                
                var iframes = _driver.FindElements(By.TagName("iframe"));
                Log($"Iframes: {iframes.Count}");
                
                foreach (var iframe in iframes)
                {
                    try
                    {
                        if (iframe.Displayed)
                        {
                            _driver.SwitchTo().Frame(iframe);
                            Log("Cambiado a iframe visible");
                            Thread.Sleep(200);
                            return;
                        }
                    }
                    catch { }
                }
                
                Log("Sin iframe visible, usando contexto principal");
            }
            catch (Exception ex)
            {
                Log($"Error SwitchToEmitidosIframe: {ex.Message}");
            }
        }

        private bool SeleccionarTipoEmitidosJS(string palabra)
        {
            var claveNorm = NormalizeText(palabra).Replace("_", " ");
            Log($"  Tipo: '{palabra}'");

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    _wait!.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector("select[id*='TipoComprobante']")));
                    Thread.Sleep(10);

                    var selectEl = _driver.FindElement(By.CssSelector("select[id*='TipoComprobante']"));
                    var select = new SelectElement(selectEl);

                    foreach (var option in select.Options)
                    {
                        var optNorm = NormalizeText(option.Text);
                        if (optNorm.Contains(claveNorm))
                        {
                            select.SelectByText(option.Text);
                            Log($"  Seleccionado: {option.Text}");
                            EsperarFinAjax(1);
                            return true;
                        }
                    }

                    Log($"  Tipo '{palabra}' no encontrado");
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    Log($"  Stale element, reintentando ({attempt + 1}/3)...");
                    Thread.Sleep(15);
                }
                catch (Exception ex)
                {
                    Log($"  Error tipo (intento {attempt + 1}): {ex.Message}");
                    Thread.Sleep(10);
                }
            }
            
            Log($"  Fallo al seleccionar tipo '{palabra}' despues de 3 intentos");
            return false;
        }

        private bool NavegarEmitidos()
        {
            Log("Navegando a Emitidos...");

            for (int overallRetry = 0; overallRetry < 3; overallRetry++)
            {
                try
                {
                    _driver!.SwitchTo().DefaultContent();
                    Thread.Sleep(1000);

                    CerrarPopup();
                    CerrarDialogosPrimeraVez();
                    Thread.Sleep(500);

                    // Try clicking sri-menu to open navigation sidebar
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            var btnMenu = _wait!.Until(ExpectedConditions.ElementToBeClickable(By.Id("sri-menu")));
                            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btnMenu);
                            Thread.Sleep(1500);
                            Log("Menu abierto");
                            break;
                        }
                        catch
                        {
                            Thread.Sleep(1000);
                        }
                    }

                    // Check if EMITIDOS is already visible (FACTURACION submenu expanded from previous RECIBIDOS nav)
                    var emitidosLinks = _driver.FindElements(By.XPath("//a[contains(translate(., 'EMITIDOS', 'emitidos'), 'emitidos')]"));
                    bool emitidosVisible = emitidosLinks.Count > 0;
                    if (emitidosVisible)
                    {
                        try { emitidosVisible = emitidosLinks[0].Displayed; } catch { }
                    }

                    if (emitidosVisible)
                    {
                        Log("Emitidos ya visible, clickeando directamente...");
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", emitidosLinks[0]);
                        Thread.Sleep(3000);
                        EsperarFinAjax(10);
                        Log("Emitidos listo");
                        return true;
                    }

                    // Click FACTURACION to expand submenu
                    if (!ClickMenuItemEmitidos("FACTUR"))
                    {
                        Log("No se pudo hacer clic en FACTURACION");
                        throw new Exception("FACTURACION not found");
                    }
                    Thread.Sleep(1500);

                    // Click Consultas
                    if (!ClickMenuItemEmitidos("Consultas"))
                    {
                        Log("No se pudo hacer clic en Consultas");
                        throw new Exception("Consultas not found");
                    }
                    Thread.Sleep(1500);

                    // Click Emitidos
                    for (int retry = 0; retry < 3; retry++)
                    {
                        try
                        {
                            var emitidos = _driver.FindElements(By.XPath("//a[contains(translate(., 'EMITIDOS', 'emitidos'), 'emitidos')]"));
                            if (emitidos.Count > 0)
                            {
                                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", emitidos[0]);
                                Thread.Sleep(3000);
                                EsperarFinAjax(10);
                                Log("Emitidos listo");
                                return true;
                            }
                        }
                        catch { }
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error navegacion (intento {overallRetry + 1}): {ex.Message}");
                }

                // Fallback: refresh and try again
                Log($"Refrescando pagina e intentando de nuevo ({overallRetry + 1}/3)...");
                try
                {
                    _driver?.Navigate().Refresh();
                    Thread.Sleep(5000);

                    for (int i = 0; i < 5; i++)
                    {
                        CerrarDialogosPrimeraVez();
                        if (!CerrarModalCaptcha()) break;
                        Thread.Sleep(500);
                    }
                }
                catch { }
            }

            return false;
        }

        private bool ClickMenuItemEmitidos(string text)
        {
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    var items = _driver.FindElements(By.XPath($"//a[.//span[contains(.,'{text}')]]"));
                    if (items.Count > 0)
                    {
                        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", items[0]);
                        return true;
                    }
                    Thread.Sleep(1000);
                }
                catch
                {
                    Thread.Sleep(1000);
                }
            }
            return false;
        }

        private List<int> GetDiasRango(SriDownloadOptions options)
        {
            var dias = new List<int>();
            
            // Si no hay fechas específicas o las fechas están fuera del mes seleccionado, usar todo el mes
            if (!options.FechaDesde.HasValue || !options.FechaHasta.HasValue)
            {
                int lastDay = DateTime.DaysInMonth(options.Anio, options.Mes);
                for (int d = 1; d <= lastDay; d++)
                    dias.Add(d);
            }
            else
            {
                for (var fecha = options.FechaDesde.Value; fecha <= options.FechaHasta.Value; fecha = fecha.AddDays(1))
                {
                    if (fecha.Year == options.Anio && fecha.Month == options.Mes)
                        dias.Add(fecha.Day);
                }
                
                // Si ningún día cayó en el mes seleccionado, usar todo el mes
                if (dias.Count == 0)
                {
                    int lastDay = DateTime.DaysInMonth(options.Anio, options.Mes);
                    for (int d = 1; d <= lastDay; d++)
                        dias.Add(d);
                }
            }
            return dias;
        }

        private void ClickConsultarEmitidos()
        {
            try
            {
                if (_driver == null) return;
                
                // Switch to iframe context
                _driver.SwitchTo().DefaultContent();
                
                var iframes = _driver.FindElements(By.TagName("iframe"));
                foreach (var iframe in iframes)
                {
                    try { if (iframe.Displayed) { _driver.SwitchTo().Frame(iframe); break; } }
                    catch { }
                }

                // Intentar múltiples selectores para el botón Consultar
                string[] xpaths = new[] {
                    "//button[.//span[contains(.,'Consultar')]]",
                    "//a[.//span[contains(.,'Consultar')]]",
                    "//button[contains(@class,'ui-button')][.//span[contains(.,'Consultar')]]",
                    "//*[contains(@id,'consultar') or contains(@id,'Consultar')]"
                };
                
                IWebElement? btn = null;
                foreach (var xpath in xpaths)
                {
                    var found = _driver.FindElements(By.XPath(xpath));
                    if (found.Count > 0)
                    {
                        btn = found[0];
                        break;
                    }
                }
                
                if (btn == null)
                {
                    // Último recurso: buscar por texto
                    var allButtons = _driver.FindElements(By.TagName("button"));
                    foreach (var b in allButtons)
                    {
                        if (b.Text.Contains("Consultar"))
                        {
                            btn = b;
                            break;
                        }
                    }
                }
                
                if (btn == null)
                {
                    Log("  Boton Consultar NO encontrado");
                    return;
                }
                
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", btn);
                Thread.Sleep(10);
                
                // Click via JavaScript
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
                Log("  Click realizado");
                
                // Esperar carga de datos
                Thread.Sleep(10);
                EsperarFinAjax(10);
                Log("  Respuesta recibida");
            }
            catch (Exception ex)
            {
                Log($"  Error consultar: {ex.Message}");
            }
        }

        private void DescargarRidesEmitidos(int totalFilas)
        {
            if (_driver == null) return;
            
            Log($"  Descargando {totalFilas} documentos...");
            
            for (int i = 0; i < totalFilas; i++)
            {
                try
                {
                    // Extraer datos del popup
                    var datos = ExtraerDatosYDescargarPDF(i + 1);
                    
                    if (datos != null && datos.ContainsKey("claveAcceso") && datos["claveAcceso"].Length >= 49)
                    {
                        Log($"  Doc {i + 1}: OK");
                        GenerarXmlDesdeDatos(datos);
                        DescargarPdfConClave(datos["claveAcceso"]);
                    }
                    else
                    {
                        Log($"  Doc {i + 1}: Sin datos");
                    }
                }
                catch (Exception ex)
                {
                    Log($"  Doc {i + 1}: Error - {ex.Message}");
                }
            }
        }

        private void DescargarPdfConClave(string claveAcceso)
        {
            try
            {
                // Generar nombre basado en la clave de acceso
                var tipoDoc = claveAcceso.Substring(8, 2);
                var codEstab = claveAcceso.Substring(10, 3);
                var ptoEmi = claveAcceso.Substring(13, 3);
                var secuencial = claveAcceso.Substring(16, 9);
                var ruc = claveAcceso.Substring(25, 13);
                var fechaCod = claveAcceso.Substring(38, 8);
                
                var tipoNombre = tipoDoc switch
                {
                    "01" => "FACTURA",
                    "04" => "NOTA_CREDITO",
                    "05" => "NOTA_DEBITO",
                    "07" => "RETENCION_VENTA",
                    _ => "DOC"
                };
                
                var fechaIso = $"{fechaCod.Substring(0,4)}-{fechaCod.Substring(4,2)}-{fechaCod.Substring(6,2)}";
                var nombrePdf = $"{tipoNombre}-{codEstab}-{ptoEmi}-{secuencial}-{ruc}-{fechaIso}.pdf";
                var fullPath = Path.Combine(_downloadDir, nombrePdf);
                
                // Si ya existe, no descargar de nuevo
                if (File.Exists(fullPath)) return;
                
                // Usar JavaScript para abrir el PDF en nueva ventana y cerrarla
                ((IJavaScriptExecutor)_driver).ExecuteScript(@"
                    var pdfUrl = '/comprobantes-electronicos-internet/pages/consultaRide.xhtml?claveAcceso=" + claveAcceso + @"';
                    window.open(pdfUrl, '_blank');
                ");
                
                // Esperar un poco para que se inicie la descarga
                Thread.Sleep(10);
                EsperarArchivo(10);
                
                // Cerrar la pestaña del PDF
                CerrarUltimaVentana();
                
                // Renombrar el PDF descargado
                RenombrarUltimoPdf(nombrePdf);
            }
            catch { }
        }

        private void CerrarUltimaVentana()
        {
            try
            {
                if (_driver.WindowHandles.Count > 1)
                {
                    var mainWindow = _driver.WindowHandles[0];
                    var lastWindow = _driver.WindowHandles[_driver.WindowHandles.Count - 1];
                    _driver.SwitchTo().Window(lastWindow);
                    _driver.Close();
                    _driver.SwitchTo().Window(mainWindow);
                }
            }
            catch { }
        }

        private void RenombrarUltimoPdf(string nombreDestino)
        {
            try
            {
                EsperarArchivo(5);
                
                var pdfFiles = Directory.GetFiles(_downloadDir, "*.pdf");
                if (pdfFiles.Length == 0) return;
                
                var lastPdf = pdfFiles.OrderByDescending(f => File.GetCreationTime(f)).First();
                var destPath = Path.Combine(_downloadDir, nombreDestino);
                
                if (File.Exists(destPath))
                    File.Delete(lastPdf);
                else
                    File.Move(lastPdf, destPath);
            }
            catch { }
        }

        private Dictionary<string, string>? ExtraerDatosYDescargarPDF(int filaNum)
        {
            try
            {
                var datos = new Dictionary<string, string>();
                
                // Encontrar la fila
                var filas = _driver.FindElements(By.XPath("//tbody[@id='frmPrincipal:tablaCompEmitidos_data']/tr"));
                if (filaNum > filas.Count) return null;
                
                var fila = filas[filaNum - 1];
                var tds = fila.FindElements(By.TagName("td"));
                if (tds.Count < 3) return null;
                
                // Click en la clave de acceso para abrir popup (usar Selenium click)
                var celdaClave = tds[2];
                var link = celdaClave.FindElements(By.TagName("a"));
                
                if (link.Count > 0)
                {
                    SafeClick(link[0]);
                }
                else
                {
                    SafeClick(celdaClave);
                }
                
                // Esperar popup
                Thread.Sleep(10);
                
                // Extraer datos via PageSource
                var pageSource = _driver.PageSource;
                
                datos["claveAcceso"] = Extract(pageSource, @"<label>Clave de acceso\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["fechaEmision"] = Extract(pageSource, @"<label>Fecha Emisión\s*</label></td>\s*<td[^>]*><label>([\d/]+)</label>");
                datos["razonSocial"] = Extract(pageSource, @"<label>Razón Social\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["ruc"] = Extract(pageSource, @"<label>Número RUC\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["estab"] = Extract(pageSource, @"<label>Establecimiento\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["ptoEmi"] = Extract(pageSource, @"<label>Punto de emisión\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["secuencial"] = Extract(pageSource, @"<label>Secuencial\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["razonSocialComprador"] = Extract(pageSource, @"<label>Razón Social Comprador\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["identificacionComprador"] = Extract(pageSource, @"<label>Identificación Comprador\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["tipoIdentificacionComprador"] = Extract(pageSource, @"<label>Tipo Identificación Comprador\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");

                datos["razonSocialSujetoRetenido"] = Extract(pageSource, @"<label>Razón Social(?: de)? Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["identificacionSujetoRetenido"] = Extract(pageSource, @"<label>Identificación Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["tipoIdentificacionSujetoRetenido"] = Extract(pageSource, @"<label>Tipo Identificación Sujeto Retenido\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");

                datos["totalSinImpuestos"] = Extract(pageSource, @"<label>Total Sin impuestos\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                datos["importeTotal"] = Extract(pageSource, @"<label>Importe Total\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                datos["ambiente"] = Extract(pageSource, @"<label>Ambiente\s*</label></td>\s*<td[^>]*><label>(\d)</label>");
                datos["tipoEmision"] = Extract(pageSource, @"<label>Tipo de emisión\s*</label></td>\s*<td[^>]*><label>(\d)</label>");
                
                // Extraer tipo de documento de la clave de acceso
                if (!string.IsNullOrEmpty(datos["claveAcceso"]) && datos["claveAcceso"].Length >= 10)
                {
                    datos["tipoDoc"] = datos["claveAcceso"].Substring(8, 2);
                }
                
                // Extraer campos adicionales
                datos["codDocModificado"] = Extract(pageSource, @"<label>Cod Doc Modificado\s*</label></td>\s*<td[^>]*><label>(\d+)</label>");
                datos["numDocModificado"] = Extract(pageSource, @"<label>Num Doc Modificado\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["fechaEmisionDocSustento"] = Extract(pageSource, @"<label>Fecha Emision Doc Sustento\s*</label></td>\s*<td[^>]*><label>([\d/]+)</label>");
                datos["valorModificacion"] = Extract(pageSource, @"<label>Valor Modificacion\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                datos["motivo"] = Extract(pageSource, @"<label>Motivo\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                datos["periodoFiscal"] = Extract(pageSource, @"<label>Periodo Fiscal\s*</label></td>\s*<td[^>]*><label>([^<]+)</label>");
                
                if (datos.GetValueOrDefault("tipoDoc") == "07")
                {
                    if (string.IsNullOrEmpty(datos.GetValueOrDefault("importeTotal", "")))
                        datos["importeTotal"] = Extract(pageSource, @"<label>Total\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                    if (string.IsNullOrEmpty(datos.GetValueOrDefault("totalSinImpuestos", "")))
                        datos["totalSinImpuestos"] = Extract(pageSource, @"<label>Subtotal\s*</label></td>\s*<td[^>]*><label>([\d.]+)</label>");
                    ExtraerRetencionesDesdeLabels(pageSource, datos);
                }
                
                // Cerrar popup
                try
                {
                    var closeBtn = _driver.FindElement(By.CssSelector(".ui-dialog-titlebar-close"));
                    SafeClick(closeBtn);
                }
                catch { }
                
                Thread.Sleep(5);
                
                // Validar que tenemos clave de acceso
                if (string.IsNullOrEmpty(datos["claveAcceso"]) || datos["claveAcceso"].Length < 49)
                    return null;
                
                return datos;
            }
            catch (Exception ex)
            {
                try
                {
                    var closeBtn = _driver.FindElement(By.CssSelector(".ui-dialog-titlebar-close"));
                    SafeClick(closeBtn);
                }
                catch { }
                return null;
            }
        }

        private void GenerarXmlDesdeDatos(Dictionary<string, string> datos)
        {
            try
            {
                var clave = datos.GetValueOrDefault("claveAcceso", "");
                
                // Validar que tenemos datos mínimos
                if (string.IsNullOrEmpty(clave) || clave.Length < 49)
                    return;
                
                var fecha = datos.GetValueOrDefault("fechaEmision", "01/01/2026");
                var tipoDoc = datos.GetValueOrDefault("tipoDoc", "01");
                var ruc = datos.GetValueOrDefault("ruc", "");
                
                // Convertir fecha de dd/MM/yyyy a yyyy-MM-dd
                var fechaParts = fecha.Split('/');
                var fechaIso = fechaParts.Length == 3 ? $"{fechaParts[2]}-{fechaParts[1]}-{fechaParts[0]}" : fecha;
                
                // Determinar el tipo de comprobante y estructura XML
                string comprobanteXml;
                string tipoNombre;
                
                switch (tipoDoc)
                {
                    case "01": // Factura
                        comprobanteXml = GenerarXmlFactura(datos, clave, fecha);
                        tipoNombre = "FACTURA";
                        break;
                    case "04": // Nota Crédito
                        comprobanteXml = GenerarXmlNotaCredito(datos, clave, fecha);
                        tipoNombre = "NOTA_CREDITO";
                        break;
                    case "05": // Nota Débito
                        comprobanteXml = GenerarXmlNotaDebito(datos, clave, fecha);
                        tipoNombre = "NOTA_DEBITO";
                        break;
                    case "07": // Retención
                        comprobanteXml = GenerarXmlRetencion(datos, clave, fecha);
                        tipoNombre = "RETENCION";
                        break;
                    default:
                        Log($"    Tipo de documento no soportado: {tipoDoc}");
                        return;
                }
                
                var xmlFinal = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<autorizacion>
  <estado>AUTORIZADO</estado>
  <numeroAutorizacion>{clave}</numeroAutorizacion>
  <fechaAutorizacion>{fecha}</fechaAutorizacion>
  <comprobante><![CDATA[{comprobanteXml}]]></comprobante>
</autorizacion>";

                var nombreXml = GenerarNombreBaseDesdeClave(new Dictionary<string, string> { 
                    { "clave", clave }, 
                    { "fecha", fechaIso },
                    { "ruc", datos.GetValueOrDefault("ruc", "") },
                    { "tipo", tipoNombre },
                    { "establecimiento", datos.GetValueOrDefault("estab", "000") },
                    { "secuencial", datos.GetValueOrDefault("secuencial", "000000000") }
                }) + ".xml";

                var xmlPath = Path.Combine(_downloadDir, nombreXml);
                xmlPath = NombreUnico(xmlPath);
                
                File.WriteAllText(xmlPath, xmlFinal, System.Text.Encoding.UTF8);
                Log($"    XML ({tipoNombre}): {Path.GetFileName(xmlPath)}");
            }
            catch (Exception ex)
            {
                Log($"    Error generando XML: {ex.Message}");
            }
        }

        private string GenerarXmlFactura(Dictionary<string, string> datos, string clave, string fecha)
        {
            var ivaTarifa = datos.GetValueOrDefault("ivaTarifa", "15");
            var ivaBase = datos.GetValueOrDefault("ivaBase", datos.GetValueOrDefault("totalSinImpuestos", "0.00"));
            var ivaValor = datos.GetValueOrDefault("ivaValor", "0.00");
            var codigoPorcentaje = ivaTarifa == "15" ? "4" : "0";
            
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<factura id=""comprobante"" version=""1.0.0"">
  <infoTributaria>
    <ambiente>{datos.GetValueOrDefault("ambiente", "2")}</ambiente>
    <tipoEmision>{datos.GetValueOrDefault("tipoEmision", "1")}</tipoEmision>
    <razonSocial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</razonSocial>
    <nombreComercial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</nombreComercial>
    <ruc>{datos.GetValueOrDefault("ruc", "")}</ruc>
    <claveAcceso>{clave}</claveAcceso>
    <codDoc>01</codDoc>
    <estab>{datos.GetValueOrDefault("estab", "000")}</estab>
    <ptoEmi>{datos.GetValueOrDefault("ptoEmi", "000")}</ptoEmi>
    <secuencial>{datos.GetValueOrDefault("secuencial", "000000000")}</secuencial>
    <dirMatriz>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirMatriz>
  </infoTributaria>
  <infoFactura>
    <fechaEmision>{fecha}</fechaEmision>
    <dirEstablecimiento>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirEstablecimiento>
    <obligadoContabilidad>SI</obligadoContabilidad>
    <tipoIdentificacionComprador>{datos.GetValueOrDefault("tipoIdentificacionComprador", "04")}</tipoIdentificacionComprador>
    <razonSocialComprador>{EscapeXml(datos.GetValueOrDefault("razonSocialComprador", ""))}</razonSocialComprador>
    <identificacionComprador>{datos.GetValueOrDefault("identificacionComprador", "")}</identificacionComprador>
    <totalSinImpuestos>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</totalSinImpuestos>
    <totalDescuento>0.00</totalDescuento>
    <totalConImpuestos>
      <totalImpuesto>
        <codigo>2</codigo>
        <codigoPorcentaje>{codigoPorcentaje}</codigoPorcentaje>
        <baseImponible>{ivaBase}</baseImponible>
        <valor>{ivaValor}</valor>
      </totalImpuesto>
    </totalConImpuestos>
    <importeTotal>{datos.GetValueOrDefault("importeTotal", "0.00")}</importeTotal>
    <moneda>DOLAR</moneda>
  </infoFactura>
  <detalles>
    <detalle>
      <codigoPrincipal>001</codigoPrincipal>
      <descripcion>Servicios</descripcion>
      <cantidad>1.00</cantidad>
      <precioUnitario>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</precioUnitario>
      <descuento>0.00</descuento>
      <precioTotalSinImpuesto>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</precioTotalSinImpuesto>
      <impuestos>
        <impuesto>
          <codigo>2</codigo>
          <codigoPorcentaje>{codigoPorcentaje}</codigoPorcentaje>
          <tarifa>{ivaTarifa}.00</tarifa>
          <baseImponible>{ivaBase}</baseImponible>
          <valor>{ivaValor}</valor>
        </impuesto>
      </impuestos>
    </detalle>
  </detalles>
</factura>";
        }

        private string GenerarTotalImpuestos(Dictionary<string, string> datos)
        {
            var sb = new StringBuilder();
            var countStr = datos.GetValueOrDefault("impuestoCount", "0");
            if (int.TryParse(countStr, out var count) && count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var codigo = datos.GetValueOrDefault($"impuesto{i}_codigo", "2");
                    var tarifa = datos.GetValueOrDefault($"impuesto{i}_tarifa", "0");
                    var baseImp = datos.GetValueOrDefault($"impuesto{i}_base", "0.00");
                    var valor = datos.GetValueOrDefault($"impuesto{i}_valor", "0.00");
                    
                    // CALCULAR IVA AUTOMÁTICAMENTE si hay base pero no hay valor
                    if (codigo == "2" && valor == "0.00" && baseImp != "0.00" && decimal.TryParse(baseImp, out decimal baseIVA))
                    {
                        decimal pct = 15.00m;
                        if (tarifa == "0") pct = 0;
                        else if (decimal.TryParse(tarifa, out decimal tarifaDec)) pct = tarifaDec;
                        decimal valorCalc = Math.Round(baseIVA * (pct / 100m), 2);
                        valor = valorCalc.ToString("F2", CultureInfo.InvariantCulture);
                        Log($"    Calculando IVA automático: base={baseImp}, tarifa={tarifa}, valor={valor}");
                    }
                    
                    sb.Append($@"
	      <totalImpuesto>
	        <codigo>{codigo}</codigo>
	        <codigoPorcentaje>{tarifa}</codigoPorcentaje>
	        <baseImponible>{baseImp}</baseImponible>
	        <valor>{valor}</valor>
	      </totalImpuesto>");
                }
            }
            else
            {
                sb.Append($@"
	      <totalImpuesto>
	        <codigo>2</codigo>
	        <codigoPorcentaje>0</codigoPorcentaje>
	        <baseImponible>0.00</baseImponible>
	        <valor>0.00</valor>
	      </totalImpuesto>");
            }
            return sb.ToString();
        }
        
        private string GenerarDetalleImpuestos(Dictionary<string, string> datos)
        {
            var sb = new StringBuilder();
            var countStr = datos.GetValueOrDefault("impuestoCount", "0");
            if (int.TryParse(countStr, out var count) && count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var codigo = datos.GetValueOrDefault($"impuesto{i}_codigo", "2");
                    var tarifa = datos.GetValueOrDefault($"impuesto{i}_tarifa", "0");
                    var baseImp = datos.GetValueOrDefault($"impuesto{i}_base", "0.00");
                    var valor = datos.GetValueOrDefault($"impuesto{i}_valor", "0.00");
                    
                    // CALCULAR IVA AUTOMÁTICAMENTE si hay base pero no hay valor
                    if (codigo == "2" && valor == "0.00" && baseImp != "0.00" && decimal.TryParse(baseImp, out decimal baseIVA))
                    {
                        decimal pct = 15.00m;
                        if (tarifa == "0") pct = 0;
                        else if (decimal.TryParse(tarifa, out decimal tarifaDec)) pct = tarifaDec;
                        decimal valorCalc = Math.Round(baseIVA * (pct / 100m), 2);
                        valor = valorCalc.ToString("F2", CultureInfo.InvariantCulture);
                    }
                    
                    sb.Append($@"
			<impuesto>
			  <codigo>{codigo}</codigo>
			  <codigoPorcentaje>{tarifa}</codigoPorcentaje>
			  <tarifa>{tarifa}</tarifa>
			  <baseImponible>{baseImp}</baseImponible>
			  <valor>{valor}</valor>
			</impuesto>");
                }
            }
            return sb.ToString();
        }
        
        private string GenerarXmlNotaCredito(Dictionary<string, string> datos, string clave, string fecha)
        {
            var agenteRet = datos.GetValueOrDefault("agenteRetencion", "");
            var agenteRetencionTag = !string.IsNullOrEmpty(agenteRet) ? $"\n\t<agenteRetencion>{agenteRet}</agenteRetencion>" : "";
            
            var totalImpuestos = GenerarTotalImpuestos(datos);
            var detalleImpuestos = GenerarDetalleImpuestos(datos);
            
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<notaCredito id=""comprobante"" version=""1.1.0"">
	<infoTributaria>
		<ambiente>{datos.GetValueOrDefault("ambiente", "2")}</ambiente>
	    <tipoEmision>{datos.GetValueOrDefault("tipoEmision", "1")}</tipoEmision>
	    <razonSocial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</razonSocial>
	    <nombreComercial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</nombreComercial>
	    <ruc>{datos.GetValueOrDefault("ruc", "")}</ruc>
	    <claveAcceso>{clave}</claveAcceso>
	    <codDoc>04</codDoc>
	    <estab>{datos.GetValueOrDefault("estab", "000")}</estab>
	    <ptoEmi>{datos.GetValueOrDefault("ptoEmi", "000")}</ptoEmi>
	    <secuencial>{datos.GetValueOrDefault("secuencial", "000000000")}</secuencial>
	    <dirMatriz>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirMatriz>{agenteRetencionTag}
  	</infoTributaria>
	<infoNotaCredito>
	    <fechaEmision>{fecha}</fechaEmision>
	    <dirEstablecimiento>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirEstablecimiento>
	    <tipoIdentificacionComprador>{datos.GetValueOrDefault("tipoIdentificacionComprador", "04")}</tipoIdentificacionComprador>
	    <razonSocialComprador>{EscapeXml(datos.GetValueOrDefault("razonSocialComprador", ""))}</razonSocialComprador>
	    <identificacionComprador>{datos.GetValueOrDefault("identificacionComprador", "")}</identificacionComprador>
	    <obligadoContabilidad>SI</obligadoContabilidad>
	    <codDocModificado>{datos.GetValueOrDefault("codDocModificado", "01")}</codDocModificado>
	    <numDocModificado>{datos.GetValueOrDefault("numDocModificado", "")}</numDocModificado>
	    <fechaEmisionDocSustento>{datos.GetValueOrDefault("fechaEmisionDocSustento", fecha)}</fechaEmisionDocSustento>
	    <totalSinImpuestos>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</totalSinImpuestos>
	    <valorModificacion>{datos.GetValueOrDefault("valorModificacion", datos.GetValueOrDefault("importeTotal", "0.00"))}</valorModificacion>
	    <moneda>DOLAR</moneda>
	    <totalConImpuestos>
{totalImpuestos}
	    </totalConImpuestos>
	    <motivo>{EscapeXml(datos.GetValueOrDefault("motivo", "DEVOLUCION"))}</motivo>
  	</infoNotaCredito>
  	<detalles>
    <detalle>
      <codigoInterno>001</codigoInterno>
      <descripcion>{EscapeXml(datos.GetValueOrDefault("motivo", "Ajuste"))}</descripcion>
      <cantidad>1.000000</cantidad>
      <precioUnitario>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</precioUnitario>
      <descuento>0.00</descuento>
      <precioTotalSinImpuesto>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</precioTotalSinImpuesto>
	  <impuestos>
{detalleImpuestos}
	  </impuestos>
    </detalle>
    </detalles>
</notaCredito>";
        }

        private string GenerarXmlNotaDebito(Dictionary<string, string> datos, string clave, string fecha)
        {
            var totalImpuestos = GenerarTotalImpuestos(datos);
            var detalleImpuestos = GenerarDetalleImpuestos(datos);
            
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<notaDebito id=""comprobante"" version=""1.1.0"">
	<infoTributaria>
		<ambiente>{datos.GetValueOrDefault("ambiente", "2")}</ambiente>
	    <tipoEmision>{datos.GetValueOrDefault("tipoEmision", "1")}</tipoEmision>
	    <razonSocial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</razonSocial>
	    <nombreComercial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</nombreComercial>
	    <ruc>{datos.GetValueOrDefault("ruc", "")}</ruc>
	    <claveAcceso>{clave}</claveAcceso>
	    <codDoc>05</codDoc>
	    <estab>{datos.GetValueOrDefault("estab", "000")}</estab>
	    <ptoEmi>{datos.GetValueOrDefault("ptoEmi", "000")}</ptoEmi>
	    <secuencial>{datos.GetValueOrDefault("secuencial", "000000000")}</secuencial>
	    <dirMatriz>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirMatriz>
  	</infoTributaria>
	<infoNotaDebito>
	    <fechaEmision>{fecha}</fechaEmision>
	    <dirEstablecimiento>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirEstablecimiento>
	    <tipoIdentificacionComprador>{datos.GetValueOrDefault("tipoIdentificacionComprador", "04")}</tipoIdentificacionComprador>
	    <razonSocialComprador>{EscapeXml(datos.GetValueOrDefault("razonSocialComprador", ""))}</razonSocialComprador>
	    <identificacionComprador>{datos.GetValueOrDefault("identificacionComprador", "")}</identificacionComprador>
	    <obligadoContabilidad>SI</obligadoContabilidad>
	    <codDocModificado>{datos.GetValueOrDefault("codDocModificado", "01")}</codDocModificado>
	    <numDocModificado>{datos.GetValueOrDefault("numDocModificado", "")}</numDocModificado>
	    <fechaEmisionDocSustento>{datos.GetValueOrDefault("fechaEmisionDocSustento", fecha)}</fechaEmisionDocSustento>
	    <totalSinImpuestos>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</totalSinImpuestos>
	    <valorAumento>{datos.GetValueOrDefault("valorModificacion", datos.GetValueOrDefault("importeTotal", "0.00"))}</valorAumento>
	    <moneda>DOLAR</moneda>
	    <totalConImpuestos>
{totalImpuestos}
	    </totalConImpuestos>
	    <motivo>{EscapeXml(datos.GetValueOrDefault("motivo", "CORRECCION"))}</motivo>
  	</infoNotaDebito>
  	<detalles>
    <detalle>
      <codigoInterno>001</codigoInterno>
      <descripcion>{EscapeXml(datos.GetValueOrDefault("motivo", "Ajuste"))}</descripcion>
      <cantidad>1.000000</cantidad>
      <precioUnitario>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</precioUnitario>
      <descuento>0.00</descuento>
      <precioTotalSinImpuesto>{datos.GetValueOrDefault("totalSinImpuestos", "0.00")}</precioTotalSinImpuesto>
	  <impuestos>
{detalleImpuestos}
	  </impuestos>
    </detalle>
    </detalles>
</notaDebito>";
        }

        private string GenerarXmlRetencion(Dictionary<string, string> datos, string clave, string fecha)
        {
            var contribEspecial = datos.GetValueOrDefault("contribuyenteEspecial", "");
            var contribTag = !string.IsNullOrEmpty(contribEspecial) ? $"\n<contribuyenteEspecial>{contribEspecial}</contribuyenteEspecial>" : "";
            
            var periodoFiscal = datos.GetValueOrDefault("periodoFiscal", "");
            if (string.IsNullOrEmpty(periodoFiscal) && fecha.Length >= 10)
                periodoFiscal = fecha.Substring(3, 2) + "/" + fecha.Substring(6, 4);
            
            var docsSustentoXml = GenerarDocsSustentoRetencion(datos);
            
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<comprobanteRetencion id=""comprobante"" version=""2.0.0"">
<infoTributaria>
<ambiente>{datos.GetValueOrDefault("ambiente", "2")}</ambiente>
<tipoEmision>{datos.GetValueOrDefault("tipoEmision", "1")}</tipoEmision>
<razonSocial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</razonSocial>
<nombreComercial>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</nombreComercial>
<ruc>{datos.GetValueOrDefault("ruc", "")}</ruc>
<claveAcceso>{clave}</claveAcceso>
<codDoc>07</codDoc>
<estab>{datos.GetValueOrDefault("estab", "000")}</estab>
<ptoEmi>{datos.GetValueOrDefault("ptoEmi", "000")}</ptoEmi>
<secuencial>{datos.GetValueOrDefault("secuencial", "000000000")}</secuencial>
<dirMatriz>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirMatriz>
</infoTributaria>
<infoCompRetencion>
<fechaEmision>{fecha}</fechaEmision>
<dirEstablecimiento>{EscapeXml(datos.GetValueOrDefault("razonSocial", ""))}</dirEstablecimiento>{contribTag}
<obligadoContabilidad>SI</obligadoContabilidad>
<tipoIdentificacionSujetoRetenido>{EscapeXml(datos.GetValueOrDefault("tipoIdentificacionSujetoRetenido", datos.GetValueOrDefault("tipoIdentificacionComprador", "04")))}</tipoIdentificacionSujetoRetenido>
<parteRel>NO</parteRel>
<razonSocialSujetoRetenido>{EscapeXml(datos.GetValueOrDefault("razonSocialSujetoRetenido", datos.GetValueOrDefault("razonSocialComprador", "")))}</razonSocialSujetoRetenido>
<identificacionSujetoRetenido>{EscapeXml(datos.GetValueOrDefault("identificacionSujetoRetenido", datos.GetValueOrDefault("identificacionComprador", "")))}</identificacionSujetoRetenido>
<periodoFiscal>{periodoFiscal}</periodoFiscal>
</infoCompRetencion>
{docsSustentoXml}
</comprobanteRetencion>";
        }
        
        private string GenerarDocsSustentoRetencion(Dictionary<string, string> datos)
        {
            var sb = new StringBuilder();
            var retCountStr = datos.GetValueOrDefault("retencionCount", "0");
            int.TryParse(retCountStr, out var retCount);
            
            var numDocSustento = datos.GetValueOrDefault("numDocSustento", "");
            if (string.IsNullOrEmpty(numDocSustento))
                numDocSustento = datos.GetValueOrDefault("numDocModificado", "000000000");
            
            var fechaDocSustento = datos.GetValueOrDefault("fechaEmisionDocSustento", "");
            var totalSinImp = datos.GetValueOrDefault("totalSinImpuestos", "0.00");
            var importeTotal = datos.GetValueOrDefault("importeTotal", "0.00");
            
            sb.Append("<docsSustento>\n<docSustento>\n");
            sb.Append($"<codSustento>02</codSustento>\n");
            sb.Append($"<codDocSustento>01</codDocSustento>\n");
            sb.Append($"<numDocSustento>{EscapeXml(numDocSustento)}</numDocSustento>\n");
            sb.Append($"<fechaEmisionDocSustento>{fechaDocSustento}</fechaEmisionDocSustento>\n");
            sb.Append("<pagoLocExt>01</pagoLocExt>\n");
            sb.Append($"<totalSinImpuestos>{totalSinImp}</totalSinImpuestos>\n");
            sb.Append($"<importeTotal>{importeTotal}</importeTotal>\n");
            
            // impuestosDocSustento - IVA del documento sustento
            var impCountStr = datos.GetValueOrDefault("impuestoCount", "0");
            var retBaseStr = datos.GetValueOrDefault("retencion0_base", "0.00");
            if (!int.TryParse(impCountStr, out var impCount) || impCount == 0)
            {
                if (!string.IsNullOrEmpty(retBaseStr) && retBaseStr != "0.00")
                {
                    impCount = 1;
                    impCountStr = "1";
                }
            }
            if (impCount > 0)
            {
                sb.Append("<impuestosDocSustento>\n");
                for (int i = 0; i < impCount; i++)
                {
                    var codImp = datos.GetValueOrDefault($"impuesto{i}_codigo", "2");
                    var baseImp = datos.GetValueOrDefault($"impuesto{i}_base", retBaseStr);
                    var tarifa = datos.GetValueOrDefault($"impuesto{i}_tarifa", "15");
                    var valor = datos.GetValueOrDefault($"impuesto{i}_valor", "0.00");
                    var codPorc = tarifa == "0" ? "0" : tarifa == "12" ? "3" : "4";
                    
                    sb.Append("<impuestoDocSustento>\n");
                    sb.Append($"<codImpuestoDocSustento>{codImp}</codImpuestoDocSustento>\n");
                    sb.Append($"<codigoPorcentaje>{codPorc}</codigoPorcentaje>\n");
                    sb.Append($"<baseImponible>{baseImp}</baseImponible>\n");
                    sb.Append($"<tarifa>{tarifa}</tarifa>\n");
                    sb.Append($"<valorImpuesto>{valor}</valorImpuesto>\n");
                    sb.Append("</impuestoDocSustento>\n");
                }
                sb.Append("</impuestosDocSustento>\n");
            }
            
            // retenciones
            if (retCount > 0)
            {
                sb.Append("<retenciones>\n");
                for (int i = 0; i < retCount; i++)
                {
                    var codigo = datos.GetValueOrDefault($"retencion{i}_codigo", "1");
                    var baseImp = datos.GetValueOrDefault($"retencion{i}_base", "0.00");
                    var porcentaje = datos.GetValueOrDefault($"retencion{i}_porcentaje", "0");
                    var valor = datos.GetValueOrDefault($"retencion{i}_valor", "0.00");
                    
                    sb.Append("<retencion>\n");
                    sb.Append($"<codigo>{codigo}</codigo>\n");
                    sb.Append($"<codigoRetencion>{porcentaje}</codigoRetencion>\n");
                    sb.Append($"<baseImponible>{baseImp}</baseImponible>\n");
                    sb.Append($"<porcentajeRetener>{porcentaje}</porcentajeRetener>\n");
                    sb.Append($"<valorRetenido>{valor}</valorRetenido>\n");
                    sb.Append("</retencion>\n");
                }
                sb.Append("</retenciones>\n");
            }
            else
            {
                var baseFallback = !string.IsNullOrEmpty(totalSinImp) ? totalSinImp : importeTotal;
                if (string.IsNullOrEmpty(baseFallback)) baseFallback = "0.00";
                sb.Append("<retenciones>\n");
                sb.Append($@"<retencion>
<codigo>1</codigo>
<codigoRetencion>344</codigoRetencion>
<baseImponible>{baseFallback}</baseImponible>
<porcentajeRetener>10</porcentajeRetener>
<valorRetenido>0.00</valorRetenido>
</retencion>
");
                sb.Append("</retenciones>\n");
            }
            
            // pagos
            sb.Append("<pagos>\n<pago>\n");
            sb.Append("<formaPago>20</formaPago>\n");
            sb.Append($"<total>{importeTotal}</total>\n");
            sb.Append("</pago>\n</pagos>\n");
            
            sb.Append("</docSustento>\n</docsSustento>");
            
            return sb.ToString();
        }

        private string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private void MoverArchivosADirectorio(string dirDestino)
        {
            int movidos = 0;
            
            // Mover XMLs
            foreach (var file in Directory.GetFiles(_downloadDir, "*.xml"))
            {
                try
                {
                    var destFile = Path.Combine(dirDestino, Path.GetFileName(file));
                    if (!File.Exists(destFile))
                    {
                        File.Move(file, destFile);
                        movidos++;
                    }
                    else
                        File.Delete(file);
                }
                catch { }
            }

            // Mover PDFs
            foreach (var file in Directory.GetFiles(_downloadDir, "*.pdf"))
            {
                try
                {
                    var destFile = Path.Combine(dirDestino, Path.GetFileName(file));
                    if (!File.Exists(destFile))
                    {
                        File.Move(file, destFile);
                        movidos++;
                    }
                    else
                        File.Delete(file);
                }
                catch { }
            }
            
            if (movidos > 0)
                Log($"  Movidos {movidos} archivos a {Path.GetFileName(dirDestino)}");
        }

        private void CerrarDriver()
        {
            try
            {
                if (_driver != null)
                {
                    try { _driver.Close(); } catch { }
                    try { _driver.Quit(); } catch { }
                }
            }
            catch { }
            finally
            {
                _driver = null;
            }

            // Limpiar perfil temporal (como Python hace)
            if (!string.IsNullOrEmpty(_tempProfileDir) && Directory.Exists(_tempProfileDir))
            {
                try
                {
                    Directory.Delete(_tempProfileDir, true);
                    Log($"Perfil temporal eliminado: {_tempProfileDir}");
                }
                catch (Exception ex)
                {
                    Log($"Error eliminando perfil temporal: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                CerrarDriver();
                _disposed = true;
            }
        }
    }
}
