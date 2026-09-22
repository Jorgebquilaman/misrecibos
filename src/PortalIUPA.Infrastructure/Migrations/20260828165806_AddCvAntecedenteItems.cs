using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvAntecedenteItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cv_antecedente_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seccion = table.Column<int>(type: "integer", nullable: false),
                    categoria = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    titulo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    institucion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_carga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_antecedente_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cv_item_adjuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_item_adjuntos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cv_antecedente_items_empleado_id_seccion",
                table: "cv_antecedente_items",
                columns: new[] { "empleado_id", "seccion" });

            migrationBuilder.CreateIndex(
                name: "IX_cv_item_adjuntos_adjunto_id",
                table: "cv_item_adjuntos",
                column: "adjunto_id");

            migrationBuilder.CreateIndex(
                name: "IX_cv_item_adjuntos_item_id",
                table: "cv_item_adjuntos",
                column: "item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_antecedente_items");

            migrationBuilder.DropTable(
                name: "cv_item_adjuntos");
        }
    }
}
