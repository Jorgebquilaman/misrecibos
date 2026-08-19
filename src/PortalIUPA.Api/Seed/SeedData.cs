using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.ValueObjects;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Api.Seed;

/// <summary>Datos de desarrollo: áreas, tipos de licencia con niveles, períodos, empleados con roles y relaciones.</summary>
public static class SeedData
{
    public static async Task EjecutarAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Empleados.AnyAsync())
        {
            logger.LogInformation("Base ya tiene datos; se omite el seed.");
            return;
        }

        logger.LogInformation("Sembrando datos de desarrollo...");

        var rectoria = new Area("Rectoría", "RECTORIA");
        var secretaria = new Area("Secretaría Académica", "SEC_ACAD");
        var rrhh = new Area("RRHH", "RRHH");
        var administracion = new Area("Administración", "ADMIN");
        var direccionCarreras = new Area("Dirección de Carreras", "DIR_CARR", secretaria.Id);
        var tecnologia = new Area("Tecnología Educativa", "TEC_EDU", secretaria.Id);
        var espacios = new Area("Espacios y Servicios", "ESP_SERV", administracion.Id);

        db.Areas.AddRange(rectoria, secretaria, rrhh, administracion, direccionCarreras, tecnologia, espacios);

        var particular = new TipoLicencia("Particular", limiteMensual: 5, limiteAnual: 20,
            "Licencia por asuntos particulares.");
        var estudio = new TipoLicencia("Examen / Estudio", limiteMensual: null, limiteAnual: 10,
            "Rendir exámenes o preparación.");
        var medicaCorta = new TipoLicencia("Licencia médica (hasta 7 días)", limiteMensual: null, limiteAnual: 21,
            "Certificado médico de hasta 7 días corridos.", requiereAdjunto: true);
        var medicaLarga = new TipoLicencia("Licencia médica (más de 7 días)", limiteMensual: null, limiteAnual: null,
            "Certificado médico de más de 7 días corridos.", requiereAdjunto: true);
        var maternidad = new TipoLicencia("Maternidad / Paternidad", limiteMensual: null, limiteAnual: null,
            "Licencia especial por nacimiento.", requiereAdjunto: true);

        medicaCorta.AgregarNivel(AprobadorRequerido.ResponsableDirecto);
        medicaLarga.AgregarNivel(AprobadorRequerido.ResponsableDirecto);
        medicaLarga.AgregarNivel(AprobadorRequerido.Rrhh);
        maternidad.AgregarNivel(AprobadorRequerido.ResponsableDirecto);
        maternidad.AgregarNivel(AprobadorRequerido.Rrhh);
        estudio.AgregarNivel(AprobadorRequerido.ResponsableDirecto);

        db.TiposLicencia.AddRange(particular, estudio, medicaCorta, medicaLarga, maternidad);

        var julio = new Periodo("2026-07", "Julio 2026", 166);
        julio.Activar();
        var junio = new Periodo("2026-06", "Junio 2026", 163);
        junio.Activar();
        db.Periodos.AddRange(
            new Periodo("2026-04", "Abril 2026", 160),
            new Periodo("2026-05", "Mayo 2026", 162),
            junio,
            julio);

        var admin = new Empleado(1, "Sistema", "Administrador", new Email("administrador@iupa.edu.ar"),
            "30111111", "20-30111111-1");
        admin.AsignarArea(rrhh.Id);
        admin.AgregarRol(Rol.Administrador);

        var responsableRrhh = new Empleado(2, "Laura", "Fernández", new Email("rrhh@iupa.edu.ar"),
            "30222222", "27-30222222-3");
        responsableRrhh.AsignarArea(rrhh.Id);
        responsableRrhh.AgregarRol(Rol.Rrhh);
        responsableRrhh.AgregarRol(Rol.Responsable);

        var jefeCarreras = new Empleado(3, "Martín", "López", new Email("martin.lopez@iupa.edu.ar"),
            "30333333", "20-30333333-5");
        jefeCarreras.AsignarArea(direccionCarreras.Id);
        jefeCarreras.AgregarRol(Rol.Responsable);

        var director = new Empleado(4, "Silvia", "Ramos", new Email("silvia.ramos@iupa.edu.ar"),
            "30444444", "27-30444444-7");
        director.AsignarArea(rectoria.Id);
        director.AgregarRol(Rol.Direccion);

        var ana = new Empleado(5, "Ana", "García", new Email("ana.garcia@iupa.edu.ar"), "30555555",
            "27-30555555-9");
        ana.AsignarArea(direccionCarreras.Id);
        var carlos = new Empleado(6, "Carlos", "Pérez", new Email("carlos.perez@iupa.edu.ar"), "30666666",
            "20-30666666-1");
        carlos.AsignarArea(direccionCarreras.Id);
        var maria = new Empleado(7, "María", "Torres", new Email("maria.torres@iupa.edu.ar"), "30777777",
            "27-30777777-3");
        maria.AsignarArea(tecnologia.Id);
        var juan = new Empleado(8, "Juan", "Díaz", new Email("juan.diaz@iupa.edu.ar"), "30888888",
            "20-30888888-5");
        juan.AsignarArea(espacios.Id);

        db.Empleados.AddRange(admin, responsableRrhh, jefeCarreras, director, ana, carlos, maria, juan);

        db.RelacionesACargo.AddRange(
            new RelacionACargo(jefeCarreras.Id, ana.Id, autorizaMarcas: true, new DateOnly(2024, 3, 1)),
            new RelacionACargo(jefeCarreras.Id, carlos.Id, autorizaMarcas: true, new DateOnly(2024, 3, 1)),
            new RelacionACargo(director.Id, maria.Id, autorizaMarcas: false, new DateOnly(2024, 3, 1)),
            new RelacionACargo(admin.Id, juan.Id, autorizaMarcas: false, new DateOnly(2024, 3, 1)));

        db.Anuncios.Add(new Anuncio("Bienvenidos al Portal del Empleado",
            "A partir de hoy el portal reemplaza a MisRecibos: recibos de sueldo, licencias, fichadas, " +
            "anuncios y certificados laborales desde un solo lugar. Cualquier duda, contacte a RRHH.",
            admin.Id, PrioridadAnuncio.Urgente, TipoAnuncio.Informativo, DateOnly.FromDateTime(DateTime.Today)));

        db.Notificaciones.Add(new Notificacion(ana.Id, TipoNotificacion.Sistema,
            "Bienvenido al portal", "Puede descargar sus recibos desde la solapa Recibos."));

        await db.SaveChangesAsync();
        logger.LogInformation("Seed completado (8 empleados, 7 áreas, 5 tipos de licencia, 3 períodos).");
    }
}