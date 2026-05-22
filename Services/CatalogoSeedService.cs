using AtsManager.Pages.Empresas.Models;
using Microsoft.EntityFrameworkCore;

namespace AtsManager.Services
{
    public class CatalogoSeedService
    {
        private readonly AtsDbContext _context;

        public CatalogoSeedService(AtsDbContext context)
        {
            _context = context;
        }

        public async Task SeedCatalogsAsync()
        {
            await SeedTipoComprobanteAsync();
            await SeedTipoIdentificacionAsync();
            await SeedSustentosAsync();
            await SeedFormasPagoAsync();
            await SeedAirAsync();
            await SeedPaisesAsync();
            await SeedTiposProveedorClienteAsync();
            await SeedTiposRegimenExteriorAsync();
            await SeedTiposEmisionAsync();
            await _context.SaveChangesAsync();
        }

        private async Task SeedTipoComprobanteAsync()
        {
            if (await _context.CatTiposComprobante.AnyAsync()) return;

            var tipos = new List<CatTipoComprobante>
            {
                new() { Codigo = "01", Nombre = "FACTURA", NombreCorto = "Factura", EsNotaCreditoDebito = false },
                new() { Codigo = "02", Nombre = "LIQUIDACIÓN DE COMPRA DE BIENES Y PRESTACIÓN DE SERVICIOS", NombreCorto = "Liquidación", EsNotaCreditoDebito = false },
                new() { Codigo = "03", Nombre = "NOTA DE DÉBITO", NombreCorto = "Nota Débito", EsNotaCreditoDebito = true, RequiereDocModificado = true },
                new() { Codigo = "04", Nombre = "NOTA DE CRÉDITO", NombreCorto = "Nota Crédito", EsNotaCreditoDebito = true, RequiereDocModificado = true },
                new() { Codigo = "05", Nombre = "GUÍA DE REMISIÓN", NombreCorto = "Guía", EsNotaCreditoDebito = false },
                new() { Codigo = "06", Nombre = "COMPROBANTE DE RETENCIÓN", NombreCorto = "Retención", EsNotaCreditoDebito = false },
                new() { Codigo = "07", Nombre = "COMPROBANTE DE RETENCIÓN COMPRA", NombreCorto = "Retención", EsNotaCreditoDebito = false },
            };
            _context.CatTiposComprobante.AddRange(tipos);
        }

        private async Task SeedTipoIdentificacionAsync()
        {
            if (await _context.CatTiposIdentificacion.AnyAsync()) return;

            var tipos = new List<CatTipoIdentificacion>
            {
                new() { Codigo = "04", Nombre = "RUC" },
                new() { Codigo = "05", Nombre = "CÉDULA" },
                new() { Codigo = "06", Nombre = "PASAPORTE" },
                new() { Codigo = "07", Nombre = "IDENTIFICADOR DEL EXTERIOR" },
                new() { Codigo = "08", Nombre = "PLACA" },
            };
            _context.CatTiposIdentificacion.AddRange(tipos);
        }

        private async Task SeedSustentosAsync()
        {
            if (await _context.CatSustentos.AnyAsync()) return;

            var sustentos = new List<CatSustento>
            {
                new() { Codigo = "00", Descripcion = "SIN SUSTENTO TRIBUTARIO" },
                new() { Codigo = "01", Descripcion = "COSTO O GASTO" },
                new() { Codigo = "02", Descripcion = "ACTIVO FIJO" },
                new() { Codigo = "03", Descripcion = "ACTIVO BIOLÓGICO" },
                new() { Codigo = "04", Descripcion = "GASTO PERSONAL" },
                new() { Codigo = "05", Descripcion = "PAGO A TRABAJADORES" },
                new() { Codigo = "06", Descripcion = "SEGUROS Y REASEGUROS" },
                new() { Codigo = "07", Descripcion = "PROVEEDORES" },
                new() { Codigo = "08", Descripcion = "DEDUCCIÓN DE VIAJES, ALOJAMIENTO Y ALIMENTACIÓN" },
                new() { Codigo = "09", Descripcion = "DEDUCCIÓN DE ARRENDAMIENTO" },
                new() { Codigo = "10", Descripcion = "OTROS GASTOS" },
                new() { Codigo = "15", Descripcion = "SERVICIOS DIGITALES" },
                new() { Codigo = "16", Descripcion = "OTROS SERVICIOS" },
            };
            _context.CatSustentos.AddRange(sustentos);
        }

        private async Task SeedFormasPagoAsync()
        {
            if (await _context.CatFormasPago.AnyAsync()) return;

            var formas = new List<CatFormaPago>
            {
                new() { Codigo = "01", Nombre = "SIN UTILIZACIÓN DEL SISTEMA FINANCIERO", RequiereMonto = false },
                new() { Codigo = "02", Nombre = "CHEQUE PROPIO", RequiereMonto = true },
                new() { Codigo = "03", Nombre = "CHEQUE DE GERENCIA", RequiereMonto = true },
                new() { Codigo = "04", Nombre = "DÉBITO EN CUENTA", RequiereMonto = true },
                new() { Codigo = "05", Nombre = "CRÉDITO EN CUENTA", RequiereMonto = true },
                new() { Codigo = "06", Nombre = "TARJETA DE DÉBITO", RequiereMonto = true },
                new() { Codigo = "07", Nombre = "TARJETA DE CRÉDITO", RequiereMonto = true },
                new() { Codigo = "08", Nombre = "DINERO ELECTRÓNICO", RequiereMonto = true },
                new() { Codigo = "09", Nombre = "VALE DE EFECTIVO", RequiereMonto = true },
                new() { Codigo = "10", Nombre = "OTROS", RequiereMonto = true },
                new() { Codigo = "20", Nombre = "COMPENSACIÓN DE DEUDAS", RequiereMonto = true },
                new() { Codigo = "21", Nombre = "ENDOSO DE TÍTULOS", RequiereMonto = true },
            };
            _context.CatFormasPago.AddRange(formas);
        }

        private async Task SeedAirAsync()
        {
            if (await _context.CatAires.AnyAsync()) return;

            var aires = new List<CatAir>
            {
                new() { Codigo = "302", Descripcion = "Rentas del trabajo en relación de dependencia", Porcentaje = "0" },
                new() { Codigo = "303", Descripcion = "Rentas del trabajo con relación de dependencia - Rentas exentas", Porcentaje = "0" },
                new() { Codigo = "304", Descripcion = "Servicios profesionales por honorarios, comisiones y demás", Porcentaje = "10" },
                new() { Codigo = "305", Descripcion = "Servicios profesionales por honorarios, comisiones - Rentas exentas", Porcentaje = "0" },
                new() { Codigo = "306", Descripcion = "Jubilados y pensionistas", Porcentaje = "0" },
                new() { Codigo = "307", Descripcion = "Remuneración variable y/O accidental", Porcentaje = "0" },
                new() { Codigo = "308", Descripcion = "Ingresos por.arriendos", Porcentaje = "5" },
                new() { Codigo = "309", Descripcion = "Ingresos por.arriendos - Rentas exentas", Porcentaje = "0" },
                new() { Codigo = "310", Descripcion = "Ingresos por venta de bienes muebles", Porcentaje = "1" },
                new() { Codigo = "311", Descripcion = "Ingresos por venta de bienes inmuebles", Porcentaje = "0" },
                new() { Codigo = "312", Descripcion = "Otros ingresos", Porcentaje = "10" },
                new() { Codigo = "313", Descripcion = "Intereses y rendimientos financieros", Porcentaje = "10" },
                new() { Codigo = "314", Descripcion = "Intereses y rendimientos por mora", Porcentaje = "10" },
                new() { Codigo = "315", Descripcion = "Dividendos, utilidades o beneficio extras", Porcentaje = "0" },
                new() { Codigo = "318", Descripcion = "Loterías, rifas, apuestas y similares", Porcentaje = "15" },
                new() { Codigo = "319", Descripcion = "Contratos de строительства", Porcentaje = "5" },
                new() { Codigo = "320", Descripcion = "Propiedad intelectual", Porcentaje = "10" },
                new() { Codigo = "321", Descripcion = "Servicios de transporte", Porcentaje = "1" },
                new() { Codigo = "322", Descripcion = "Comisiones y demás pagos por работодатель", Porcentaje = "8" },
                new() { Codigo = "323", Descripcion = "Pagos por participación в юрисдикции", Porcentaje = "10" },
                new() { Codigo = "324", Descripcion = "Pagos a без учета gastos", Porcentaje = "10" },
                new() { Codigo = "325", Descripcion = "Pagos a Trabajadores Eventuales", Porcentaje = "0" },
                new() { Codigo = "326", Descripcion = "Servicios de связанные con технологией информацией", Porcentaje = "10" },
                new() { Codigo = "327", Descripcion = "Pagos por промежуточный финансовых инструментов", Porcentaje = "10" },
                new() { Codigo = "328", Descripcion = "Pagos Fondos de Inversión y fondos Complementary", Porcentaje = "10" },
                new() { Codigo = "329", Descripcion = "Pagos por Arrendamiento Mercantil", Porcentaje = "1" },
                new() { Codigo = "330", Descripcion = "Anticipo по договору страхования", Porcentaje = "10" },
                new() { Codigo = "331", Descripcion = "Sueldos y salarios", Porcentaje = "0" },
                new() { Codigo = "332", Descripcion = "Honorarios profesionales", Porcentaje = "10" },
                new() { Codigo = "333", Descripcion = "Servicios especializados, enseñanza militar", Porcentaje = "0" },
                new() { Codigo = "334", Descripcion = "Ingresos percibidos por entidades públicas", Porcentaje = "0" },
                new() { Codigo = "335", Descripcion = "Ingresos percibidos por entidades educativas", Porcentaje = "0" },
                new() { Codigo = "336", Descripcion = "Pensiones", Porcentaje = "0" },
                new() { Codigo = "337", Descripcion = "Viajes, hospedaje y alimentación", Porcentaje = "0" },
                new() { Codigo = "338", Descripcion = "Indemnizaciones por despido", Porcentaje = "0" },
                new() { Codigo = "339", Descripcion = "Bonificaciones, participación a trabajadores", Porcentaje = "0" },
                new() { Codigo = "340", Descripcion = "Venta de combustibles", Porcentaje = "0" },
                new() { Codigo = "341", Descripcion = "Enajenación de derechos representativos de capital", Porcentaje = "0" },
                new() { Codigo = "342", Descripcion = "Herencias, legados y donaciones", Porcentaje = "0" },
                new() { Codigo = "343", Descripcion = "Loterías, rifas, apuestas y similares - Premio", Porcentaje = "0" },
                new() { Codigo = "344", Descripcion = "Emisión de uangа pasajes", Porcentaje = "0" },
                new() { Codigo = "345", Descripcion = "Operaciones con在香港 юрисдикции", Porcentaje = "0" },
                new() { Codigo = "346", Descripcion = "Operaciones сосредоточены en paraísos fiscales", Porcentaje = "0" },
            };
            _context.CatAires.AddRange(aires);
        }

        private async Task SeedPaisesAsync()
        {
            if (await _context.CatPaises.AnyAsync()) return;

            var paises = new List<CatPais>
            {
                new() { Codigo = "593", Nombre = "ECUADOR", EsParaisoFiscal = false },
                new() { Codigo = "170", Nombre = "COLOMBIA", EsParaisoFiscal = false },
                new() { Codigo = "604", Nombre = "PERÚ", EsParaisoFiscal = false },
                new() { Codigo = "032", Nombre = "ARGENTINA", EsParaisoFiscal = false },
                new() { Codigo = "724", Nombre = "ESPAÑA", EsParaisoFiscal = false },
                new() { Codigo = "840", Nombre = "ESTADOS UNIDOS", EsParaisoFiscal = false },
                new() { Codigo = "250", Nombre = "FRANCIA", EsParaisoFiscal = false },
                new() { Codigo = "276", Nombre = "ALEMANIA", EsParaisoFiscal = false },
                new() { Codigo = "826", Nombre = "REINO UNIDO", EsParaisoFiscal = false },
                new() { Codigo = "124", Nombre = "CANADÁ", EsParaisoFiscal = false },
                new() { Codigo = "036", Nombre = "AUSTRALIA", EsParaisoFiscal = false },
                new() { Codigo = "392", Nombre = "JAPÓN", EsParaisoFiscal = false },
                new() { Codigo = "156", Nombre = "CHINA", EsParaisoFiscal = false },
                new() { Codigo = "704", Nombre = "VIETNAM", EsParaisoFiscal = false },
                new() { Codigo = "458", Nombre = "MALASIA", EsParaisoFiscal = false },
                new() { Codigo = "702", Nombre = "SINGAPUR", EsParaisoFiscal = true },
                new() { Codigo = "528", Nombre = "PAÍSES BAJOS", EsParaisoFiscal = false },
                new() { Codigo = "752", Nombre = "SUECIA", EsParaisoFiscal = false },
                new() { Codigo = "756", Nombre = "SUIZA", EsParaisoFiscal = true },
                new() { Codigo = "380", Nombre = "ITALIA", EsParaisoFiscal = false },
                new() { Codigo = "470", Nombre = "MALTA", EsParaisoFiscal = true },
                new() { Codigo = "660", Nombre = "ANTIGUA Y BARBUDA", EsParaisoFiscal = true },
                new() { Codigo = "136", Nombre = "ISLAS CAIMÁN", EsParaisoFiscal = true },
                new() { Codigo = "192", Nombre = "CUBA", EsParaisoFiscal = false },
                new() { Codigo = "862", Nombre = "VENEZUELA", EsParaisoFiscal = false },
            };
            _context.CatPaises.AddRange(paises);
        }

        private async Task SeedTiposProveedorClienteAsync()
        {
            if (await _context.CatTiposProveedorCliente.AnyAsync()) return;

            var tipos = new List<CatTipoProveedorCliente>
            {
                new() { Codigo = "01", Nombre = " PERSONA NATURAL" },
                new() { Codigo = "02", Nombre = "SOCIEDADES" },
                new() { Codigo = "03", Nombre = "ENTIDADES PÚBLICAS" },
                new() { Codigo = "04", Nombre = "ENTIDADES EXTRANJERAS" },
            };
            _context.CatTiposProveedorCliente.AddRange(tipos);
        }

        private async Task SeedTiposRegimenExteriorAsync()
        {
            if (await _context.CatTiposRegimenExterior.AnyAsync()) return;

            var regimenes = new List<CatTipoRegimenExterior>
            {
                new() { Codigo = "01", Nombre = "RESIDENTE" },
                new() { Codigo = "02", Nombre = "ESTABLECIMIENTO PERMANENTE" },
                new() { Codigo = "03", Nombre = "NO RESIDENTE" },
            };
            _context.CatTiposRegimenExterior.AddRange(regimenes);
        }

        private async Task SeedTiposEmisionAsync()
        {
            if (await _context.CatTiposEmision.AnyAsync()) return;

            var tipos = new List<CatTipoEmision>
            {
                new() { Codigo = "1", Nombre = "EMISIÓN NORMAL" },
                new() { Codigo = "2", Nombre = "EMISIÓN POR CONTINGENCIA" },
            };
            _context.CatTiposEmision.AddRange(tipos);
        }
    }
}
