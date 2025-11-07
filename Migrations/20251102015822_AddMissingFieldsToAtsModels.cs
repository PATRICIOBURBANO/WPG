using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtsManager.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingFieldsToAtsModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveAcceso",
                table: "Ventas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumento",
                table: "CargasLotes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaveAcceso",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "CargasLotes");
        }
    }
}
