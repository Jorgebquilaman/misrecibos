using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.Auth;
using PortalIUPA.Infrastructure.External.Email;
using PortalIUPA.Infrastructure.External.Exports;
using PortalIUPA.Infrastructure.External.Jasper;
using PortalIUPA.Infrastructure.External.Reloj;
using PortalIUPA.Infrastructure.External.SiuMapuche;
using PortalIUPA.Infrastructure.External.Storage;
using PortalIUPA.Infrastructure.Pdf;
using PortalIUPA.Infrastructure.Persistence;
using PortalIUPA.Infrastructure.Persistence.Repositories;

namespace PortalIUPA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Persistencia
        services.AddDbContext<AppDbContext>(opciones =>
            opciones.UseNpgsql(configuration.GetConnectionString("PortalIUPA")));

        services.AddScoped<IEmpleadoRepository, EmpleadoRepository>();
        services.AddScoped<IAreaRepository, AreaRepository>();
        services.AddScoped<IRelacionACargoRepository, RelacionACargoRepository>();
        services.AddScoped<IPeriodoRepository, PeriodoRepository>();
        services.AddScoped<IDescargaReciboRepository, DescargaReciboRepository>();
        services.AddScoped<ITipoLicenciaRepository, TipoLicenciaRepository>();
        services.AddScoped<ISolicitudLicenciaRepository, SolicitudLicenciaRepository>();
        services.AddScoped<IAprobacionRepository, AprobacionRepository>();
        services.AddScoped<IAdjuntoRepository, AdjuntoRepository>();
        services.AddScoped<IMarcaRelojRepository, MarcaRelojRepository>();
        services.AddScoped<IAnuncioRepository, AnuncioRepository>();
        services.AddScoped<IAnuncioLeidoRepository, AnuncioLeidoRepository>();
        services.AddScoped<INotificacionRepository, NotificacionRepository>();
        services.AddScoped<ICertificadoLaboralRepository, CertificadoLaboralRepository>();
        services.AddScoped<ICertificadoCvRepository, CertificadoCvRepository>();
        services.AddScoped<ICvExperienciaRepository, CvExperienciaRepository>();
        services.AddScoped<ICvAntecedenteAcademicoRepository, CvAntecedenteAcademicoRepository>();
        services.AddScoped<IAccesoLogRepository, AccesoLogRepository>();

        // Adaptadores externos
        services.AddOptions<JasperOptions>().Bind(configuration.GetSection("Jasper"));
        services.AddHttpClient<IJasperReportClient, JasperReportClient>(cliente =>
            cliente.Timeout = TimeSpan.FromSeconds(30));

        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailPort, SmtpEmailSender>();

        services.AddOptions<StorageOptions>().Bind(configuration.GetSection("Storage"));
        services.AddScoped<IFileStoragePort, LocalFileStorage>();

        services.AddScoped<IExportadorEmpleados, ExportadorEmpleados>();
        services.AddScoped<IExportadorTabular, ExportadorTabular>();

        services.AddOptions<RelojOptions>().Bind(configuration.GetSection("Reloj"));
        services.AddScoped<IRelojDataSource>(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<Microsoft.Extensions.Options.IOptions<RelojOptions>>().Value;
            return opciones.Fuente.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SqlServerRelojDataSource>(proveedor)
                : ActivatorUtilities.CreateInstance<MockRelojDataSource>(proveedor);
        });

        services.AddOptions<SiuMapucheOptions>().Bind(configuration.GetSection("SiuMapuche"));
        services.AddScoped<IPeriodosDataSource, SiuMapuchePeriodosDataSource>();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddScoped<IGeneradorPdfCertificado, CertificadoPdfGenerator>();
        services.AddScoped<IGeneradorPdfCv, CvPdfGenerator>();

        return services;
    }
}