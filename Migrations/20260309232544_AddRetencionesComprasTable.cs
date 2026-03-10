using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class AddRetencionesComprasTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetencionesCompras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RucEmpresa = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Anio = table.Column<short>(type: "smallint", nullable: true),
                    Mes = table.Column<short>(type: "smallint", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CargaLoteId = table.Column<int>(type: "int", nullable: true),
                    Estab = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PtoEmi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Secuencial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaRetencion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdProveedor = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    RazonSocialProveedor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NumComprobante = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    NumRetencion = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Autorizacion = table.Column<string>(type: "nvarchar(49)", maxLength: 49, nullable: false),
                    BaseImponible = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BaseImpGrav = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MontoIva = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MontoTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValRetBien10 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValRetServ20 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValorRetBienes = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValRetServ50 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValorRetServicios = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValRetServ100 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BaseImpAir = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CodRetAir = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PorcentajeAir = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValRetAir = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalRetencion = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetencionesCompras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetencionesCompras_CargasLotes_CargaLoteId",
                        column: x => x.CargaLoteId,
                        principalTable: "CargasLotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetencionesCompras_CargaLoteId",
                table: "RetencionesCompras",
                column: "CargaLoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetencionesCompras");
        }
    }
}
