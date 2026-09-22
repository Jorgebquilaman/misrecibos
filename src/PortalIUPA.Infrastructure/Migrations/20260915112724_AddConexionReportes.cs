using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConexionReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "conexion",
                table: "reportes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "PortalIUPA");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "conexion",
                table: "reportes");
        }
    }
}
