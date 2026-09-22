using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportesBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reportes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    query_sql = table.Column<string>(type: "text", nullable: false),
                    definicion = table.Column<string>(type: "jsonb", nullable: false),
                    creado_por_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reportes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reportes_permisos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reportes_permisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reportes_permisos_reportes_reporte_id",
                        column: x => x.reporte_id,
                        principalTable: "reportes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reportes_permisos_reporte_id",
                table: "reportes_permisos",
                column: "reporte_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reportes_permisos");

            migrationBuilder.DropTable(
                name: "reportes");
        }
    }
}
