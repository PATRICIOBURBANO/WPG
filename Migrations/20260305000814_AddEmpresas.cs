using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_CargasLotes_CargaLoteId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_CargasLotes_Anio_Mes_TipoArchivo",
                table: "CargasLotes");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaEmision",
                table: "Compras",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseImponible",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "TipoArchivo",
                table: "CargasLotes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ruc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CodEstablecimiento = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NCCompras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaveAcceso = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaAutorizacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SerieComprobante = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumeroDocumentoModificado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RucEmisor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RazonSocialEmisor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValorSinImpuestos = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCreacion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NCCompras", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_CargasLotes_CargaLoteId",
                table: "Ventas",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_CargasLotes_CargaLoteId",
                table: "Ventas");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropTable(
                name: "NCCompras");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaEmision",
                table: "Compras",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseImponible",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TipoArchivo",
                table: "CargasLotes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_CargasLotes_Anio_Mes_TipoArchivo",
                table: "CargasLotes",
                columns: new[] { "Anio", "Mes", "TipoArchivo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_CargasLotes_CargaLoteId",
                table: "Ventas",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id");
        }
    }
}
