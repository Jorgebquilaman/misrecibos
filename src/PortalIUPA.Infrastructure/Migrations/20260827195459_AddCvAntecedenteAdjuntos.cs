using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvAntecedenteAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cv_antecedente_adjuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    antecedente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjunto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_antecedente_adjuntos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cv_antecedente_adjuntos_adjunto_id",
                table: "cv_antecedente_adjuntos",
                column: "adjunto_id");

            migrationBuilder.CreateIndex(
                name: "IX_cv_antecedente_adjuntos_antecedente_id",
                table: "cv_antecedente_adjuntos",
                column: "antecedente_id");

            // Backfill: pobla la tabla con los antecedentes existentes, cuyo archivo se guarda en adjunto_id,
            // para que sigan apareciendo en la lista de anexos y en el PDF.
            migrationBuilder.Sql(
                "INSERT INTO cv_antecedente_adjuntos (\"Id\", antecedente_id, adjunto_id, fecha_carga) " +
                "SELECT gen_random_uuid(), a.\"Id\", a.adjunto_id, a.fecha_carga " +
                "FROM cv_antecedentes_academicos a " +
                "WHERE a.adjunto_id IS NOT NULL " +
                "AND NOT EXISTS (SELECT 1 FROM cv_antecedente_adjuntos x WHERE x.adjunto_id = a.adjunto_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_antecedente_adjuntos");
        }
    }
}
