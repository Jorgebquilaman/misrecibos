using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRelojesZk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "relojes_zk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    puerto = table.Column<int>(type: "integer", nullable: false),
                    comm_key = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    ultima_descarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ultima_cantidad = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relojes_zk", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "relojes_zk_descargas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    reloj_zk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    leidas = table.Column<int>(type: "integer", nullable: false),
                    nuevas = table.Column<int>(type: "integer", nullable: false),
                    duplicadas = table.Column<int>(type: "integer", nullable: false),
                    legajos_desconocidos = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario_correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relojes_zk_descargas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_relojes_zk_descargas_fecha",
                table: "relojes_zk_descargas",
                column: "fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "relojes_zk");

            migrationBuilder.DropTable(
                name: "relojes_zk_descargas");
        }
    }
}
