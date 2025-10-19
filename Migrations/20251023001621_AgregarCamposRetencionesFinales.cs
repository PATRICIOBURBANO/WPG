using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposRetencionesFinales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ValorRetiva",
                table: "Ventas",
                newName: "valRetRenta");

            migrationBuilder.RenameColumn(
                name: "ValorRetRenta",
                table: "Ventas",
                newName: "valRetIVA");

            migrationBuilder.AddColumn<string>(
                name: "AutorizacionRetencion",
                table: "Ventas",
                type: "nvarchar(49)",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRetencion",
                table: "Ventas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaPago",
                table: "Ventas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumRetencion",
                table: "Ventas",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutorizacionRetencion",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FechaRetencion",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FormaPago",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "NumRetencion",
                table: "Ventas");

            migrationBuilder.RenameColumn(
                name: "valRetRenta",
                table: "Ventas",
                newName: "ValorRetiva");

            migrationBuilder.RenameColumn(
                name: "valRetIVA",
                table: "Ventas",
                newName: "ValorRetRenta");
        }
    }
}
