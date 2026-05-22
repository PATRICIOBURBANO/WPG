using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCodRetAirLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Compras_CargasLotes_CargaLoteId",
                table: "Compras");

            migrationBuilder.DropForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes");

            migrationBuilder.DropForeignKey(
                name: "FK_RetencionesCompras_CargasLotes_CargaLoteId",
                table: "RetencionesCompras");

            migrationBuilder.AlterColumn<string>(
                name: "CodRetAir",
                table: "RetencionesClientes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AddColumn<string>(
                name: "AutorizacionModificada",
                table: "Compras",
                type: "nvarchar(49)",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstablecimientoModificado",
                table: "Compras",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PuntoEmisionModificado",
                table: "Compras",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecuencialModificado",
                table: "Compras",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoComprobanteModificado",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Compras_CargasLotes_CargaLoteId",
                table: "Compras",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RetencionesCompras_CargasLotes_CargaLoteId",
                table: "RetencionesCompras",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Compras_CargasLotes_CargaLoteId",
                table: "Compras");

            migrationBuilder.DropForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes");

            migrationBuilder.DropForeignKey(
                name: "FK_RetencionesCompras_CargasLotes_CargaLoteId",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "AutorizacionModificada",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "EstablecimientoModificado",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PuntoEmisionModificado",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "SecuencialModificado",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TipoComprobanteModificado",
                table: "Compras");

            migrationBuilder.AlterColumn<string>(
                name: "CodRetAir",
                table: "RetencionesClientes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddForeignKey(
                name: "FK_Compras_CargasLotes_CargaLoteId",
                table: "Compras",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RetencionesCompras_CargasLotes_CargaLoteId",
                table: "RetencionesCompras",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id");
        }
    }
}
