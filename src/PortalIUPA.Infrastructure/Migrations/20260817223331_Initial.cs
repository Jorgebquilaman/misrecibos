using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalIUPA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accesos_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    accion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    dispositivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_hora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accesos_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "adjuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    entidad_tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpleadoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjuntos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "anuncios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    prioridad = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    alcance = table.Column<int>(type: "integer", nullable: false),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rol = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anuncios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "anuncios_leidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    anuncio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_lectura = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anuncios_leidos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "aprobaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nivel_aprobacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aprobador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resultado = table.Column<int>(type: "integer", nullable: false),
                    comentario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_hora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aprobaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    area_padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_areas_areas_area_padre_id",
                        column: x => x.area_padre_id,
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificados_laborales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    desde = table.Column<DateOnly>(type: "date", nullable: false),
                    hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    archivo_adjunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_solicitud = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificados_laborales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "descargas_recibo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_descargas_recibo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marcas_reloj",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    tipo_marca = table.Column<int>(type: "integer", nullable: false),
                    origen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marcas_reloj", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    leida = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "periodos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "relaciones_a_cargo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autoriza_marcas = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relaciones_a_cargo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes_licencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_licencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    asunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    adjunto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_solicitud = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes_licencia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_licencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    limite_mensual = table.Column<int>(type: "integer", nullable: true),
                    limite_anual = table.Column<int>(type: "integer", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    requiere_adjunto = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_licencia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "empleados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    legajo = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    apellido = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    dni = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cuil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    correo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    roles = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_empleados_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "niveles_aprobacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoLicenciaId = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    rol_requerido = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_niveles_aprobacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_niveles_aprobacion_tipos_licencia_TipoLicenciaId",
                        column: x => x.TipoLicenciaId,
                        principalTable: "tipos_licencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accesos_log_fecha_hora",
                table: "accesos_log",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "IX_adjuntos_entidad_tipo_entidad_id",
                table: "adjuntos",
                columns: new[] { "entidad_tipo", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_anuncios_leidos_anuncio_id_empleado_id",
                table: "anuncios_leidos",
                columns: new[] { "anuncio_id", "empleado_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_aprobaciones_solicitud_id",
                table: "aprobaciones",
                column: "solicitud_id");

            migrationBuilder.CreateIndex(
                name: "IX_areas_area_padre_id",
                table: "areas",
                column: "area_padre_id");

            migrationBuilder.CreateIndex(
                name: "IX_areas_codigo",
                table: "areas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_descargas_recibo_empleado_id_periodo_id",
                table: "descargas_recibo",
                columns: new[] { "empleado_id", "periodo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_empleados_area_id",
                table: "empleados",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_empleados_correo",
                table: "empleados",
                column: "correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_empleados_legajo",
                table: "empleados",
                column: "legajo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marcas_reloj_empleado_id_fecha_hora",
                table: "marcas_reloj",
                columns: new[] { "empleado_id", "fecha_hora" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_niveles_aprobacion_TipoLicenciaId",
                table: "niveles_aprobacion",
                column: "TipoLicenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_empleado_id_leida",
                table: "notificaciones",
                columns: new[] { "empleado_id", "leida" });

            migrationBuilder.CreateIndex(
                name: "IX_periodos_codigo",
                table: "periodos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_relaciones_a_cargo_empleado_id",
                table: "relaciones_a_cargo",
                column: "empleado_id");

            migrationBuilder.CreateIndex(
                name: "IX_relaciones_a_cargo_responsable_id",
                table: "relaciones_a_cargo",
                column: "responsable_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_licencia_empleado_id_estado",
                table: "solicitudes_licencia",
                columns: new[] { "empleado_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_licencia_tipo_licencia_id",
                table: "solicitudes_licencia",
                column: "tipo_licencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_tipos_licencia_nombre",
                table: "tipos_licencia",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accesos_log");

            migrationBuilder.DropTable(
                name: "adjuntos");

            migrationBuilder.DropTable(
                name: "anuncios");

            migrationBuilder.DropTable(
                name: "anuncios_leidos");

            migrationBuilder.DropTable(
                name: "aprobaciones");

            migrationBuilder.DropTable(
                name: "certificados_laborales");

            migrationBuilder.DropTable(
                name: "descargas_recibo");

            migrationBuilder.DropTable(
                name: "empleados");

            migrationBuilder.DropTable(
                name: "marcas_reloj");

            migrationBuilder.DropTable(
                name: "niveles_aprobacion");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "periodos");

            migrationBuilder.DropTable(
                name: "relaciones_a_cargo");

            migrationBuilder.DropTable(
                name: "solicitudes_licencia");

            migrationBuilder.DropTable(
                name: "areas");

            migrationBuilder.DropTable(
                name: "tipos_licencia");
        }
    }
}
