using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvExperienciaAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cv_experiencia_adjuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    experiencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_experiencia_adjuntos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cv_experiencia_adjuntos_adjunto_id",
                table: "cv_experiencia_adjuntos",
                column: "adjunto_id");

            migrationBuilder.CreateIndex(
                name: "IX_cv_experiencia_adjuntos_experiencia_id",
                table: "cv_experiencia_adjuntos",
                column: "experiencia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_experiencia_adjuntos");
        }
    }
}
