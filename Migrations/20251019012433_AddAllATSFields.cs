using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class AddAllATSFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ValorRetRenta",
                table: "Compras",
                newName: "ValorRetencionNc");

            migrationBuilder.AlterColumn<string>(
                name: "RazonSocialProveedor",
                table: "Compras",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AplicConvDobTrib",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Autorizacion",
                table: "Compras",
                type: "nvarchar(49)",
                maxLength: 49,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseImpAir",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseImpExe",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CodRetAir",
                table: "Compras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodSustento",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodigoCompra",
                table: "Compras",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DenopagoRegFis",
                table: "Compras",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRegistro",
                table: "Compras",
                type: "datetime2",
                maxLength: 10,
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "FormaPago",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PagExtSujRetNorLeg",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PagoLocExt",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaisEfecPago",
                table: "Compras",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaisEfecPagoGen",
                table: "Compras",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaisEfecPagoParFis",
                table: "Compras",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeAir",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TipoProveedor",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoRegi",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetAir",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetBien10",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetServ100",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetServ20",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValRetServ50",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AplicConvDobTrib",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "Autorizacion",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "BaseImpAir",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "BaseImpExe",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "CodRetAir",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "CodSustento",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "CodigoCompra",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "DenopagoRegFis",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "FechaRegistro",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "FormaPago",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PagExtSujRetNorLeg",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PagoLocExt",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PaisEfecPago",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PaisEfecPagoGen",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PaisEfecPagoParFis",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PorcentajeAir",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TipoProveedor",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TipoRegi",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "ValRetAir",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "ValRetBien10",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "ValRetServ100",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "ValRetServ20",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "ValRetServ50",
                table: "Compras");

            migrationBuilder.RenameColumn(
                name: "ValorRetencionNc",
                table: "Compras",
                newName: "ValorRetRenta");

            migrationBuilder.AlterColumn<string>(
                name: "RazonSocialProveedor",
                table: "Compras",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);
        }
    }
}
