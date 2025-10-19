using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class InitialAtsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CargasLotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    TipoArchivo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasLotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Compras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CargaLoteId = table.Column<int>(type: "int", nullable: true),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoComprobante = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Establecimiento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PuntoEmision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Secuencial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoIdentificacionProveedor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentificacionProveedor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RazonSocialProveedor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseImponible = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseImponibleIVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseNoObjetoIVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoIVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoIce = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HayRetencion = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_CargasLotes_CargaLoteId",
                        column: x => x.CargaLoteId,
                        principalTable: "CargasLotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CargaLoteId = table.Column<int>(type: "int", nullable: true),
                    TipoIdentificacionCliente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentificacionCliente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RazonSocialCliente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoComprobante = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseImponible = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseImponibleIVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseNoObjetoIVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoIVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ventas_CargasLotes_CargaLoteId",
                        column: x => x.CargaLoteId,
                        principalTable: "CargasLotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CargasLotes_Anio_Mes_TipoArchivo",
                table: "CargasLotes",
                columns: new[] { "Anio", "Mes", "TipoArchivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_CargaLoteId",
                table: "Compras",
                column: "CargaLoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_CargaLoteId",
                table: "Ventas",
                column: "CargaLoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Compras");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "CargasLotes");
        }
    }
}
