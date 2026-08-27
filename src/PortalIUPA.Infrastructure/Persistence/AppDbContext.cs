using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<RelacionACargo> RelacionesACargo => Set<RelacionACargo>();
    public DbSet<Periodo> Periodos => Set<Periodo>();
    public DbSet<DescargaRecibo> DescargasRecibo => Set<DescargaRecibo>();
    public DbSet<TipoLicencia> TiposLicencia => Set<TipoLicencia>();
    public DbSet<SolicitudLicencia> SolicitudesLicencia => Set<SolicitudLicencia>();
    public DbSet<Aprobacion> Aprobaciones => Set<Aprobacion>();
    public DbSet<Adjunto> Adjuntos => Set<Adjunto>();
    public DbSet<MarcaReloj> MarcasReloj => Set<MarcaReloj>();
    public DbSet<Anuncio> Anuncios => Set<Anuncio>();
    public DbSet<AnuncioLeido> AnunciosLeidos => Set<AnuncioLeido>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<CertificadoLaboral> CertificadosLaborales => Set<CertificadoLaboral>();
    public DbSet<CertificadoCurso> CertificadosCv => Set<CertificadoCurso>();
    public DbSet<CvExperiencia> CvExperiencias => Set<CvExperiencia>();
    public DbSet<CvAntecedenteAcademico> CvAntecedentesAcademicos => Set<CvAntecedenteAcademico>();
    public DbSet<AccesoLog> AccesosLog => Set<AccesoLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurarEmpleado(modelBuilder);
        ConfigurarArea(modelBuilder);
        ConfigurarRelacionACargo(modelBuilder);
        ConfigurarPeriodo(modelBuilder);
        ConfigurarDescargaRecibo(modelBuilder);
        ConfigurarTipoLicencia(modelBuilder);
        ConfigurarSolicitudLicencia(modelBuilder);
        ConfigurarAprobacion(modelBuilder);
        ConfigurarAdjunto(modelBuilder);
        ConfigurarMarcaReloj(modelBuilder);
        ConfigurarAnuncio(modelBuilder);
        ConfigurarNotificacion(modelBuilder);
        ConfigurarCertificado(modelBuilder);
        ConfigurarCertificadoCv(modelBuilder);
        ConfigurarCvExperiencia(modelBuilder);
        ConfigurarCvAntecedenteAcademico(modelBuilder);
        ConfigurarAccesoLog(modelBuilder);

        // Los DateTimes del dominio no usan un único Kind (DateTime.Now y DateTime.UtcNow conviven);
        // se normalizan a "timestamp without time zone" quitando el Kind, como en la base legacy.
        foreach (var propiedad in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            propiedad.SetColumnType("timestamp without time zone");
            propiedad.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified)));
        }
    }

    private static void ConfigurarEmpleado(ModelBuilder b)
    {
        b.Entity<Empleado>(e =>
        {
            e.ToTable("empleados");
            e.HasKey(x => x.Id);
            e.Property(x => x.Legajo).HasColumnName("legajo");
            e.HasIndex(x => x.Legajo).IsUnique();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
            e.Property(x => x.Apellido).HasColumnName("apellido").HasMaxLength(120).IsRequired();
            e.Property(x => x.Dni).HasColumnName("dni").HasMaxLength(20);
            e.Property(x => x.Cuil).HasColumnName("cuil").HasMaxLength(20);
            e.Property(x => x.Correo).HasColumnName("correo").HasMaxLength(320).IsRequired()
                .HasConversion(v => v.Valor, v => new Email(v));
            e.HasIndex(x => x.Correo).IsUnique();
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.AreaId).HasColumnName("area_id");
            e.Property(x => x.CvObservaciones).HasColumnName("cv_observaciones");
            e.Property(x => x.CvTelefono).HasColumnName("cv_telefono").HasMaxLength(50);
            e.PrimitiveCollection(x => x.Roles).HasColumnName("roles").HasColumnType("integer[]");
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarArea(ModelBuilder b)
    {
        b.Entity<Area>(a =>
        {
            a.ToTable("areas");
            a.HasKey(x => x.Id);
            a.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
            a.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(10).IsRequired();
            a.HasIndex(x => x.Codigo).IsUnique();
            a.Property(x => x.AreaPadreId).HasColumnName("area_padre_id");
            a.Property(x => x.Activa).HasColumnName("activa");
            a.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaPadreId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarRelacionACargo(ModelBuilder b)
    {
        b.Entity<RelacionACargo>(r =>
        {
            r.ToTable("relaciones_a_cargo");
            r.HasKey(x => x.Id);
            r.Property(x => x.ResponsableId).HasColumnName("responsable_id");
            r.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            r.Property(x => x.AutorizaMarcas).HasColumnName("autoriza_marcas");
            r.Property(x => x.FechaDesde).HasColumnName("fecha_desde");
            r.Property(x => x.FechaHasta).HasColumnName("fecha_hasta");
            r.HasIndex(x => x.EmpleadoId);
            r.HasIndex(x => x.ResponsableId);
        });
    }

    private static void ConfigurarPeriodo(ModelBuilder b)
    {
        b.Entity<Periodo>(p =>
        {
            p.ToTable("periodos");
            p.HasKey(x => x.Id);
            p.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
            p.HasIndex(x => x.Codigo).IsUnique();
            p.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(200);
            p.Property(x => x.Activo).HasColumnName("activo");
            p.Property(x => x.NroLiq).HasColumnName("nro_liq");
        });
    }

    private static void ConfigurarDescargaRecibo(ModelBuilder b)
    {
        b.Entity<DescargaRecibo>(d =>
        {
            d.ToTable("descargas_recibo");
            d.HasKey(x => x.Id);
            d.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            d.Property(x => x.PeriodoId).HasColumnName("periodo_id");
            d.Property(x => x.FechaHora).HasColumnName("fecha_hora");
            d.Property(x => x.Origen).HasColumnName("origen");
            d.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
            d.HasIndex(x => new { x.EmpleadoId, x.PeriodoId });
        });
    }

    private static void ConfigurarTipoLicencia(ModelBuilder b)
    {
        b.Entity<TipoLicencia>(t =>
        {
            t.ToTable("tipos_licencia");
            t.HasKey(x => x.Id);
            t.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            t.HasIndex(x => x.Nombre).IsUnique();
            t.Property(x => x.LimiteMensual).HasColumnName("limite_mensual");
            t.Property(x => x.LimiteAnual).HasColumnName("limite_anual");
            t.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(300);
            t.Property(x => x.RequiereAdjunto).HasColumnName("requiere_adjunto");
            t.Property(x => x.Activo).HasColumnName("activo");

            t.OwnsMany(x => x.Niveles, n =>
            {
                n.ToTable("niveles_aprobacion");
                n.WithOwner().HasForeignKey(x => x.TipoLicenciaId);
                n.HasKey(x => x.Id);
                n.Property(x => x.Orden).HasColumnName("orden");
                n.Property(x => x.RolRequerido).HasColumnName("rol_requerido");
            });
        });
    }

    private static void ConfigurarSolicitudLicencia(ModelBuilder b)
    {
        b.Entity<SolicitudLicencia>(s =>
        {
            s.ToTable("solicitudes_licencia");
            s.HasKey(x => x.Id);
            s.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            s.Property(x => x.TipoLicenciaId).HasColumnName("tipo_licencia_id");
            s.Property(x => x.FechaInicio).HasColumnName("fecha_inicio");
            s.Property(x => x.FechaFin).HasColumnName("fecha_fin");
            s.Property(x => x.Asunto).HasColumnName("asunto").HasMaxLength(200);
            s.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(1000);
            s.Property(x => x.AdjuntoId).HasColumnName("adjunto_id");
            s.Property(x => x.Estado).HasColumnName("estado");
            s.Property(x => x.FechaSolicitud).HasColumnName("fecha_solicitud");
            s.HasIndex(x => new { x.EmpleadoId, x.Estado });
            s.HasIndex(x => x.TipoLicenciaId);
        });
    }

    private static void ConfigurarAprobacion(ModelBuilder b)
    {
        b.Entity<Aprobacion>(a =>
        {
            a.ToTable("aprobaciones");
            a.HasKey(x => x.Id);
            a.Property(x => x.SolicitudId).HasColumnName("solicitud_id");
            a.Property(x => x.NivelAprobacionId).HasColumnName("nivel_aprobacion_id");
            a.Property(x => x.AprobadorId).HasColumnName("aprobador_id");
            a.Property(x => x.Resultado).HasColumnName("resultado");
            a.Property(x => x.Comentario).HasColumnName("comentario").HasMaxLength(500);
            a.Property(x => x.FechaHora).HasColumnName("fecha_hora");
            a.HasIndex(x => x.SolicitudId);
        });
    }

    private static void ConfigurarAdjunto(ModelBuilder b)
    {
        b.Entity<Adjunto>(a =>
        {
            a.ToTable("adjuntos");
            a.HasKey(x => x.Id);
            a.Property(x => x.NombreArchivo).HasColumnName("nombre_archivo").HasMaxLength(255).IsRequired();
            a.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100);
            a.Property(x => x.TamañoBytes).HasColumnName("tamano_bytes");
            a.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(255).IsRequired();
            a.Property(x => x.EntidadTipo).HasColumnName("entidad_tipo").HasMaxLength(50);
            a.Property(x => x.EntidadId).HasColumnName("entidad_id");
            a.HasIndex(x => new { x.EntidadTipo, x.EntidadId });
        });
    }

    private static void ConfigurarMarcaReloj(ModelBuilder b)
    {
        b.Entity<MarcaReloj>(m =>
        {
            m.ToTable("marcas_reloj");
            m.HasKey(x => x.Id);
            m.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            m.Property(x => x.FechaHora).HasColumnName("fecha_hora");
            m.Property(x => x.Tipo).HasColumnName("tipo_marca");
            m.Property(x => x.Origen).HasColumnName("origen").HasMaxLength(50);
            m.HasIndex(x => new { x.EmpleadoId, x.FechaHora }).IsUnique();
        });
    }

    private static void ConfigurarAnuncio(ModelBuilder b)
    {
        b.Entity<Anuncio>(a =>
        {
            a.ToTable("anuncios");
            a.HasKey(x => x.Id);
            a.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            a.Property(x => x.Cuerpo).HasColumnName("cuerpo").IsRequired();
            a.Property(x => x.FechaDesde).HasColumnName("fecha_desde");
            a.Property(x => x.FechaHasta).HasColumnName("fecha_hasta");
            a.Property(x => x.Prioridad).HasColumnName("prioridad");
            a.Property(x => x.Tipo).HasColumnName("tipo");
            a.Property(x => x.Alcance).HasColumnName("alcance");
            a.Property(x => x.AreaId).HasColumnName("area_id");
            a.Property(x => x.Rol).HasColumnName("rol");
            a.Property(x => x.Activo).HasColumnName("activo");
            a.Property(x => x.CreadoPor).HasColumnName("creado_por");
            a.Property(x => x.FechaCreacion).HasColumnName("fecha_creacion");
        });

        b.Entity<AnuncioLeido>(l =>
        {
            l.ToTable("anuncios_leidos");
            l.HasKey(x => x.Id);
            l.Property(x => x.AnuncioId).HasColumnName("anuncio_id");
            l.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            l.Property(x => x.FechaLectura).HasColumnName("fecha_lectura");
            l.HasIndex(x => new { x.AnuncioId, x.EmpleadoId }).IsUnique();
        });
    }

    private static void ConfigurarNotificacion(ModelBuilder b)
    {
        b.Entity<Notificacion>(n =>
        {
            n.ToTable("notificaciones");
            n.HasKey(x => x.Id);
            n.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            n.Property(x => x.Tipo).HasColumnName("tipo");
            n.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            n.Property(x => x.Cuerpo).HasColumnName("cuerpo");
            n.Property(x => x.Link).HasColumnName("link").HasMaxLength(300);
            n.Property(x => x.Leida).HasColumnName("leida");
            n.Property(x => x.FechaCreacion).HasColumnName("fecha_creacion");
            n.HasIndex(x => new { x.EmpleadoId, x.Leida });
        });
    }

    private static void ConfigurarCertificado(ModelBuilder b)
    {
        b.Entity<CertificadoLaboral>(c =>
        {
            c.ToTable("certificados_laborales");
            c.HasKey(x => x.Id);
            c.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            c.Property(x => x.Tipo).HasColumnName("tipo");
            c.Property(x => x.Desde).HasColumnName("desde");
            c.Property(x => x.Hasta).HasColumnName("hasta");
            c.Property(x => x.Destino).HasColumnName("destino").HasMaxLength(200);
            c.Property(x => x.Estado).HasColumnName("estado");
            c.Property(x => x.ArchivoAdjuntoId).HasColumnName("archivo_adjunto_id");
            c.Property(x => x.FechaSolicitud).HasColumnName("fecha_solicitud");
        });
    }

    private static void ConfigurarCertificadoCv(ModelBuilder b)
    {
        b.Entity<CertificadoCurso>(c =>
        {
            c.ToTable("certificados_cv");
            c.HasKey(x => x.Id);
            c.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            c.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            c.Property(x => x.Institucion).HasColumnName("institucion").HasMaxLength(200).IsRequired();
            c.Property(x => x.Tipo).HasColumnName("tipo");
            c.Property(x => x.FechaObtencion).HasColumnName("fecha_obtencion");
            c.Property(x => x.AdjuntoId).HasColumnName("adjunto_id");
            c.Property(x => x.Estado).HasColumnName("estado");
            c.Property(x => x.ComentarioRevision).HasColumnName("comentario_revision").HasMaxLength(500);
            c.Property(x => x.FechaCarga).HasColumnName("fecha_carga");
            c.HasIndex(x => new { x.EmpleadoId, x.Estado });
        });
    }

    private static void ConfigurarCvExperiencia(ModelBuilder b)
    {
        b.Entity<CvExperiencia>(c =>
        {
            c.ToTable("cv_experiencias");
            c.HasKey(x => x.Id);
            c.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            c.Property(x => x.Puesto).HasColumnName("puesto").HasMaxLength(200).IsRequired();
            c.Property(x => x.Institucion).HasColumnName("institucion").HasMaxLength(200).IsRequired();
            c.Property(x => x.Descripcion).HasColumnName("descripcion");
            c.Property(x => x.FechaDesde).HasColumnName("fecha_desde");
            c.Property(x => x.FechaHasta).HasColumnName("fecha_hasta");
            c.HasIndex(x => new { x.EmpleadoId, x.FechaDesde });
        });
    }

    private static void ConfigurarCvAntecedenteAcademico(ModelBuilder b)
    {
        b.Entity<CvAntecedenteAcademico>(c =>
        {
            c.ToTable("cv_antecedentes_academicos");
            c.HasKey(x => x.Id);
            c.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            c.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            c.Property(x => x.Institucion).HasColumnName("institucion").HasMaxLength(200).IsRequired();
            c.Property(x => x.Nivel).HasColumnName("nivel");
            c.Property(x => x.Descripcion).HasColumnName("descripcion");
            c.Property(x => x.FechaDesde).HasColumnName("fecha_desde");
            c.Property(x => x.FechaHasta).HasColumnName("fecha_hasta");
            c.Property(x => x.AdjuntoId).HasColumnName("adjunto_id");
            c.Property(x => x.FechaCarga).HasColumnName("fecha_carga");
            c.HasIndex(x => new { x.EmpleadoId, x.FechaDesde });
        });
    }

    private static void ConfigurarAccesoLog(ModelBuilder b)
    {
        b.Entity<AccesoLog>(l =>
        {
            l.ToTable("accesos_log");
            l.HasKey(x => x.Id);
            l.Property(x => x.EmpleadoId).HasColumnName("empleado_id");
            l.Property(x => x.Correo).HasColumnName("correo").HasMaxLength(320).IsRequired();
            l.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(50).IsRequired();
            l.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
            l.Property(x => x.Dispositivo).HasColumnName("dispositivo").HasMaxLength(200);
            l.Property(x => x.FechaHora).HasColumnName("fecha_hora");
            l.HasIndex(x => x.FechaHora);
        });
    }
}