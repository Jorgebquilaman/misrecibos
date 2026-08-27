using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvAntecedentesAcademicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cv_antecedentes_academicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    institucion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    adjunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_antecedentes_academicos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cv_antecedentes_academicos_empleado_id_fecha_desde",
                table: "cv_antecedentes_academicos",
                columns: new[] { "empleado_id", "fecha_desde" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_antecedentes_academicos");
        }
    }
}
