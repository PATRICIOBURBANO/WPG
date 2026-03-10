using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRetencionesCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "BaseImponible",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "Estab",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "NumComprobante",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "PtoEmi",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "Secuencial",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "ClaveAccesoSustento",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "RazonSocialEmisor",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "TipoComprobante",
                table: "RetencionesClientes");

            migrationBuilder.RenameColumn(
                name: "ValRetAir",
                table: "RetencionesCompras",
                newName: "ValRetRenta");

            migrationBuilder.RenameColumn(
                name: "MontoTotal",
                table: "RetencionesCompras",
                newName: "ValRetIva");

            migrationBuilder.RenameColumn(
                name: "FechaEmision",
                table: "RetencionesCompras",
                newName: "FechaDocAfectado");

            migrationBuilder.RenameColumn(
                name: "ValRetIVA",
                table: "RetencionesClientes",
                newName: "ValRetIva");

            migrationBuilder.RenameColumn(
                name: "RucEmisor",
                table: "RetencionesClientes",
                newName: "IdCliente");

            migrationBuilder.RenameColumn(
                name: "FechaAutorizacion",
                table: "RetencionesClientes",
                newName: "FechaDocAfectado");

            migrationBuilder.AlterColumn<string>(
                name: "CodRetAir",
                table: "RetencionesCompras",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "DocAfectado",
                table: "RetencionesCompras",
                type: "nvarchar(17)",
                maxLength: 17,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NumRetencionCompleto",
                table: "RetencionesCompras",
                type: "nvarchar(17)",
                maxLength: 17,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValRetRenta",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValRetIva",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaRetencion",
                table: "RetencionesClientes",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<int>(
                name: "CargaLoteId",
                table: "RetencionesClientes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseImpAir",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseImpGrav",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodRetAir",
                table: "RetencionesClientes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DocAfectado",
                table: "RetencionesClientes",
                type: "nvarchar(17)",
                maxLength: 17,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MontoIva",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeAir",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocialCliente",
                table: "RetencionesClientes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RucEmpresa",
                table: "RetencionesClientes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRetencion",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetBien10",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetServ100",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetServ20",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetServ50",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorRetBienes",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorRetServicios",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "DocAfectado",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "NumRetencionCompleto",
                table: "RetencionesCompras");

            migrationBuilder.DropColumn(
                name: "BaseImpAir",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "BaseImpGrav",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "CodRetAir",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "DocAfectado",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "MontoIva",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "PorcentajeAir",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "RazonSocialCliente",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "RucEmpresa",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "TotalRetencion",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "ValRetBien10",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "ValRetServ100",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "ValRetServ20",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "ValRetServ50",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "ValorRetBienes",
                table: "RetencionesClientes");

            migrationBuilder.DropColumn(
                name: "ValorRetServicios",
                table: "RetencionesClientes");

            migrationBuilder.RenameColumn(
                name: "ValRetRenta",
                table: "RetencionesCompras",
                newName: "ValRetAir");

            migrationBuilder.RenameColumn(
                name: "ValRetIva",
                table: "RetencionesCompras",
                newName: "MontoTotal");

            migrationBuilder.RenameColumn(
                name: "FechaDocAfectado",
                table: "RetencionesCompras",
                newName: "FechaEmision");

            migrationBuilder.RenameColumn(
                name: "ValRetIva",
                table: "RetencionesClientes",
                newName: "ValRetIVA");

            migrationBuilder.RenameColumn(
                name: "IdCliente",
                table: "RetencionesClientes",
                newName: "RucEmisor");

            migrationBuilder.RenameColumn(
                name: "FechaDocAfectado",
                table: "RetencionesClientes",
                newName: "FechaAutorizacion");

            migrationBuilder.AlterColumn<string>(
                name: "CodRetAir",
                table: "RetencionesCompras",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseImponible",
                table: "RetencionesCompras",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estab",
                table: "RetencionesCompras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NumComprobante",
                table: "RetencionesCompras",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PtoEmi",
                table: "RetencionesCompras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Secuencial",
                table: "RetencionesCompras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValRetRenta",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ValRetIVA",
                table: "RetencionesClientes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaRetencion",
                table: "RetencionesClientes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CargaLoteId",
                table: "RetencionesClientes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveAccesoSustento",
                table: "RetencionesClientes",
                type: "nvarchar(49)",
                maxLength: 49,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RazonSocialEmisor",
                table: "RetencionesClientes",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoComprobante",
                table: "RetencionesClientes",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_RetencionesClientes_CargasLotes_CargaLoteId",
                table: "RetencionesClientes",
                column: "CargaLoteId",
                principalTable: "CargasLotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
