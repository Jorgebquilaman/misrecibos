using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvExperienciasTelefono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cv_telefono",
                table: "empleados",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cv_experiencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    puesto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    institucion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_experiencias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cv_experiencias_empleado_id_fecha_desde",
                table: "cv_experiencias",
                columns: new[] { "empleado_id", "fecha_desde" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_experiencias");

            migrationBuilder.DropColumn(
                name: "cv_telefono",
                table: "empleados");
        }
    }
}
