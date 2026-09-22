using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dashboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    query_sql = table.Column<string>(type: "text", nullable: false),
                    definicion = table.Column<string>(type: "jsonb", nullable: false),
                    conexion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "PortalIUPA"),
                    creado_por_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboards", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboards");
        }
    }
}
