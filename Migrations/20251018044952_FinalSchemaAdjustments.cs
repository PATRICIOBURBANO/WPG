using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class FinalSchemaAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdentificacionCliente",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Establecimiento",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "IdentificacionProveedor",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "PuntoEmision",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "Secuencial",
                table: "Compras");

            migrationBuilder.RenameColumn(
                name: "MontoIVA",
                table: "Ventas",
                newName: "MontoIva");

            migrationBuilder.RenameColumn(
                name: "TipoIdentificacionCliente",
                table: "Ventas",
                newName: "UsuarioCreacion");

            migrationBuilder.RenameColumn(
                name: "BaseNoObjetoIVA",
                table: "Ventas",
                newName: "MontoIce");

            migrationBuilder.RenameColumn(
                name: "BaseImponibleIVA",
                table: "Ventas",
                newName: "BaseNoGraIva");

            migrationBuilder.RenameColumn(
                name: "MontoIVA",
                table: "Compras",
                newName: "MontoIva");

            migrationBuilder.RenameColumn(
                name: "TipoIdentificacionProveedor",
                table: "Compras",
                newName: "UsuarioCreacion");

            migrationBuilder.RenameColumn(
                name: "HayRetencion",
                table: "Compras",
                newName: "ParteRelacionada");

            migrationBuilder.RenameColumn(
                name: "BaseNoObjetoIVA",
                table: "Compras",
                newName: "ValorRetServicios");

            migrationBuilder.RenameColumn(
                name: "BaseImponibleIVA",
                table: "Compras",
                newName: "ValorRetRenta");

            migrationBuilder.AlterColumn<string>(
                name: "TipoComprobante",
                table: "Ventas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<short>(
                name: "Anio",
                table: "Ventas",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseImpGrav",
                table: "Ventas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "Ventas",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "IdCliente",
                table: "Ventas",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "Mes",
                table: "Ventas",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "NumComprobante",
                table: "Ventas",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoIdCliente",
                table: "Ventas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "TipoComprobante",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<short>(
                name: "Anio",
                table: "Compras",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseImpGrav",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseNoGraIva",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "Compras",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "IdProveedor",
                table: "Compras",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "Mes",
                table: "Compras",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "NumComprobante",
                table: "Compras",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoIdProveedor",
                table: "Compras",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorRetBienes",
                table: "Compras",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalRegistros",
                table: "CargasLotes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Anio",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "BaseImpGrav",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "IdCliente",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Mes",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "NumComprobante",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "TipoIdCliente",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Anio",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "BaseImpGrav",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "BaseNoGraIva",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "IdProveedor",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "Mes",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "NumComprobante",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TipoIdProveedor",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "ValorRetBienes",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "TotalRegistros",
                table: "CargasLotes");

            migrationBuilder.RenameColumn(
                name: "MontoIva",
                table: "Ventas",
                newName: "MontoIVA");

            migrationBuilder.RenameColumn(
                name: "UsuarioCreacion",
                table: "Ventas",
                newName: "TipoIdentificacionCliente");

            migrationBuilder.RenameColumn(
                name: "MontoIce",
                table: "Ventas",
                newName: "BaseNoObjetoIVA");

            migrationBuilder.RenameColumn(
                name: "BaseNoGraIva",
                table: "Ventas",
                newName: "BaseImponibleIVA");

            migrationBuilder.RenameColumn(
                name: "MontoIva",
                table: "Compras",
                newName: "MontoIVA");

            migrationBuilder.RenameColumn(
                name: "ValorRetServicios",
                table: "Compras",
                newName: "BaseNoObjetoIVA");

            migrationBuilder.RenameColumn(
                name: "ValorRetRenta",
                table: "Compras",
                newName: "BaseImponibleIVA");

            migrationBuilder.RenameColumn(
                name: "UsuarioCreacion",
                table: "Compras",
                newName: "TipoIdentificacionProveedor");

            migrationBuilder.RenameColumn(
                name: "ParteRelacionada",
                table: "Compras",
                newName: "HayRetencion");

            migrationBuilder.AlterColumn<string>(
                name: "TipoComprobante",
                table: "Ventas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AddColumn<string>(
                name: "IdentificacionCliente",
                table: "Ventas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "TipoComprobante",
                table: "Compras",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AddColumn<string>(
                name: "Establecimiento",
                table: "Compras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdentificacionProveedor",
                table: "Compras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PuntoEmision",
                table: "Compras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Secuencial",
                table: "Compras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
