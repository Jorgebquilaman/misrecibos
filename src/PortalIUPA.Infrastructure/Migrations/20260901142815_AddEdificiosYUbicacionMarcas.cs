using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEdificiosYUbicacionMarcas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "edificio_id",
                table: "marcas_reloj",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "edificio_nombre",
                table: "marcas_reloj",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitud",
                table: "marcas_reloj",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitud",
                table: "marcas_reloj",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "edificios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    latitud = table.Column<double>(type: "double precision", nullable: false),
                    longitud = table.Column<double>(type: "double precision", nullable: false),
                    radio_metros = table.Column<int>(type: "integer", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edificios", x => x.Id);
                });

            // Seed: edificios con coordenadas de referencia (APROXIMADAS - ajustar con las reales).
            migrationBuilder.Sql(
                "INSERT INTO edificios (\"Id\", nombre, latitud, longitud, radio_metros, activa) VALUES " +
                "(gen_random_uuid(), 'IUPA - Sede Central', -39.0276, -67.5863, 100, true), " +
                "(gen_random_uuid(), 'IUPA - Anexo', -39.0285, -67.5875, 100, true);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "edificios");

            migrationBuilder.DropColumn(
                name: "edificio_id",
                table: "marcas_reloj");

            migrationBuilder.DropColumn(
                name: "edificio_nombre",
                table: "marcas_reloj");

            migrationBuilder.DropColumn(
                name: "latitud",
                table: "marcas_reloj");

            migrationBuilder.DropColumn(
                name: "longitud",
                table: "marcas_reloj");
        }
    }
}
