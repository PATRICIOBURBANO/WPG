using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class addingRetencionClienteBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetencionesClientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CargaLoteId = table.Column<int>(type: "int", nullable: false),
                    RucEmisor = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    RazonSocialEmisor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TipoComprobante = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NumRetencionCompleto = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false),
                    NumRetencion = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    AutorizacionRetencion = table.Column<string>(type: "nvarchar(49)", maxLength: 49, nullable: false),
                    FechaAutorizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaRetencion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaveAccesoSustento = table.Column<string>(type: "nvarchar(49)", maxLength: 49, nullable: false),
                    ValRetRenta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValRetIVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Anio = table.Column<short>(type: "smallint", nullable: false),
                    Mes = table.Column<short>(type: "smallint", nullable: false),
                    UsuarioCreacion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetencionesClientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                        column: x => x.CargaLoteId,
                        principalTable: "CargasLotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetencionesClientes_CargaLoteId",
                table: "RetencionesClientes",
                column: "CargaLoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetencionesClientes");
        }
    }
}
