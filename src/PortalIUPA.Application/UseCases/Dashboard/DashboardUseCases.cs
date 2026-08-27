using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Application.Services;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Dashboard;

public sealed record GetDashboardEmpleadoQuery(Guid EmpleadoId) : IRequest<DashboardEmpleadoDto>;

public sealed class GetDashboardEmpleadoQueryHandler : IRequestHandler<GetDashboardEmpleadoQuery, DashboardEmpleadoDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IPeriodoRepository _periodos;
    private readonly IDescargaReciboRepository _descargas;
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tiposLicencia;
    private readonly IRelojDataSource _reloj;
    private readonly IMarcaRelojRepository _marcasManuales;
    private readonly IAnuncioRepository _anuncios;
    private readonly IAnuncioLeidoRepository _leidos;
    private readonly INotificacionRepository _notificaciones;
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICertificadoCvRepository _certificadosCv;
    private readonly ICertificadoLaboralRepository _certificados;

    public GetDashboardEmpleadoQueryHandler(IEmpleadoRepository empleados, IPeriodoRepository periodos,
        IDescargaReciboRepository descargas, ISolicitudLicenciaRepository solicitudes, ITipoLicenciaRepository tiposLicencia,
        IRelojDataSource reloj, IMarcaRelojRepository marcasManuales,
        IAnuncioRepository anuncios, IAnuncioLeidoRepository leidos, INotificacionRepository notificaciones,
        ICvExperienciaRepository experiencias, ICvAntecedenteAcademicoRepository antecedentes,
        ICertificadoCvRepository certificadosCv, ICertificadoLaboralRepository certificados)
    {
        _empleados = empleados;
        _periodos = periodos;
        _descargas = descargas;
        _solicitudes = solicitudes;
        _tiposLicencia = tiposLicencia;
        _reloj = reloj;
        _marcasManuales = marcasManuales;
        _anuncios = anuncios;
        _leidos = leidos;
        _notificaciones = notificaciones;
        _experiencias = experiencias;
        _antecedentes = antecedentes;
        _certificadosCv = certificadosCv;
        _certificados = certificados;
    }

    public async Task<DashboardEmpleadoDto> Handle(GetDashboardEmpleadoQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // Último recibo disponible
        ReciboDisponibleDto? ultimoRecibo = null;
        var periodos = (await _periodos.GetActivosAsync(ct)).OrderByDescending(p => p.Codigo).ToList();
        if (periodos.Count > 0)
        {
            var periodo = periodos[0];
            var ultima = await _descargas.GetUltimaDeEmpleadoEnPeriodoAsync(empleado.Id, periodo.Id, ct);
            ultimoRecibo = new ReciboDisponibleDto(periodo.Id, periodo.Codigo, periodo.Descripcion,
                ultima is not null, ultima?.FechaHora);
        }

        // Estado de licencias
        var solicitudes = await _solicitudes.GetByEmpleadoAsync(empleado.Id, ct);
        var pendientes = solicitudes.Count(s => s.Estado == EstadoSolicitud.EnEspera);
        var aprobadas = solicitudes.Count(s => s.Estado == EstadoSolicitud.Aprobada);
        var rechazadas = solicitudes.Count(s => s.Estado == EstadoSolicitud.Desaprobada);

        // Fichadas del mes actual (lectura en vivo del MSSQL del reloj + manuales PG)
        ResumenJornadasDto? fichadas = null;
        {
            var mesActual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var hastaMes = mesActual.AddMonths(1).AddSeconds(-1);
            IReadOnlyList<MarcaRelojCruda> marcas;
            try { marcas = await _reloj.ObtenerMarcasAsync(empleado.Legajo, mesActual, hastaMes, ct); }
            catch (RelojNoDisponibleException) { marcas = Array.Empty<MarcaRelojCruda>(); }
            var manuales = await _marcasManuales.GetByEmpleadoBetweenAsync(empleado.Id, mesActual, hastaMes, ct);
            var todas = marcas.Concat(manuales.Select(m => new MarcaRelojCruda(empleado.Legajo, m.FechaHora, m.Tipo, m.Origen)))
                .GroupBy(m => (m.FechaHora, m.Tipo)).Select(g => g.First()).OrderBy(m => m.FechaHora).ToList();
            var resumen = CalculadorJornadas.CalcularResumen(CalculadorJornadas.AgruparEnJornadas(todas));
            fichadas = new ResumenJornadasDto(resumen.DiasTrabajados, resumen.DiasConAnomalia, resumen.TotalHoras,
                resumen.PromedioHoras);
        }

        // Feed de anuncios (vigentes + aplicables, con marca de leído)
        var anuncios = await _anuncios.GetVigentesParaAsync(empleado, hoy, ct);
        var leidos = await _leidos.GetDeEmpleadoAsync(empleado.Id, ct);
        var leidosIds = leidos.Select(l => l.AnuncioId).ToHashSet();
        var feed = anuncios
            .OrderByDescending(a => a.Prioridad)
            .ThenByDescending(a => a.FechaCreacion)
            .Take(5)
            .Select(a => new AnuncioDto(a.Id, a.Titulo, a.Cuerpo, a.FechaDesde, a.FechaHasta, a.Prioridad, a.Tipo,
                a.Alcance, a.AreaId, a.Rol, a.Activo, leidosIds.Contains(a.Id), a.FechaCreacion))
            .ToList();

        var noLeidas = await _notificaciones.GetNoLeidasCountAsync(empleado.Id, ct);

        // Recibos detallado
        DashboardRecibosDto? recibosDto = null;
        try
        {
            var disponibles = new List<ReciboDisponibleDto>();
            foreach (var p in periodos)
            {
                var ult = await _descargas.GetUltimaDeEmpleadoEnPeriodoAsync(empleado.Id, p.Id, ct);
                disponibles.Add(new ReciboDisponibleDto(p.Id, p.Codigo, p.Descripcion, ult is not null, ult?.FechaHora));
            }
            var descargados = disponibles.Count(d => d.YaDescargado);
            recibosDto = new DashboardRecibosDto(periodos.Count, descargados, periodos.Count - descargados, disponibles);
        }
        catch { }

        // Licencias detallado + consumo mes actual
        DashboardLicenciasDto? licenciasDto = null;
        try
        {
            var canceladas = solicitudes.Count(s => s.Estado == EstadoSolicitud.Cancelada);
            var consumo = new List<ConsumoTipoLicenciaDto>();
            var tiposActivos = await _tiposLicencia.GetActivosAsync(ct);
            var anio = DateTime.Today.Year;
            var mes = DateTime.Today.Month;
            foreach (var t in tiposActivos)
            {
                var (cMes, cAnio) = await _solicitudes.GetConsumoAsync(empleado.Id, t.Id, anio, mes, ct);
                consumo.Add(new ConsumoTipoLicenciaDto(t.Id, t.Nombre, cMes, cAnio, t.LimiteMensual, t.LimiteAnual));
            }
            licenciasDto = new DashboardLicenciasDto(pendientes, aprobadas, rechazadas, canceladas, consumo);
        }
        catch { }

        // Fichadas detallado (con faltas y tardanzas)
        DashboardFichadasDetalladoDto? fichadasDetallado = null;
        try
        {
            if (fichadas is not null)
            {
                var mesActual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var hastaMes = mesActual.AddMonths(1).AddSeconds(-1);
                IReadOnlyList<MarcaRelojCruda> marcasDetallado;
                try { marcasDetallado = await _reloj.ObtenerMarcasAsync(empleado.Legajo, mesActual, hastaMes, ct); }
                catch (RelojNoDisponibleException) { marcasDetallado = Array.Empty<MarcaRelojCruda>(); }
                // merge manuales por si reloj caído ya devolvió vacío, igual usamos fichadas base
                var manualesDet = await _marcasManuales.GetByEmpleadoBetweenAsync(empleado.Id, mesActual, hastaMes, ct);
                var todasDet = marcasDetallado.Concat(manualesDet.Select(m => new MarcaRelojCruda(empleado.Legajo, m.FechaHora, m.Tipo, m.Origen))).GroupBy(m => (m.FechaHora, m.Tipo)).Select(g => g.First()).OrderBy(m => m.FechaHora).ToList();
                var jornadasDet = CalculadorJornadas.AgruparEnJornadas(todasDet);
                var resumenDet = CalculadorJornadas.CalcularResumen(jornadasDet);
                var diasHabiles = Enumerable.Range(1, DateTime.DaysInMonth(mesActual.Year, mesActual.Month))
                    .Select(d => new DateTime(mesActual.Year, mesActual.Month, d))
                    .Count(d => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday);
                var faltas = Math.Max(0, diasHabiles - resumenDet.DiasTrabajados);
                var tardanzas = jornadasDet.Count(j => j.Entrada is { } e && e.TimeOfDay > new TimeSpan(8, 30, 0));
                fichadasDetallado = new DashboardFichadasDetalladoDto(resumenDet.DiasTrabajados, resumenDet.DiasConAnomalia, faltas, tardanzas, resumenDet.TotalHoras, resumenDet.PromedioHoras,
                    jornadasDet.Select(j => new JornadaDto(j.Fecha, j.Entrada, j.Salida, j.Horas, j.EsAnomalia)).ToList());
            }
        }
        catch { }

        // CV
        DashboardCvDto? cvDto = null;
        try
        {
            var exps = await _experiencias.GetByEmpleadoAsync(empleado.Id, ct);
            var ants = await _antecedentes.GetByEmpleadoAsync(empleado.Id, ct);
            var certs = await _certificadosCv.GetByEmpleadoAsync(empleado.Id, ct);
            var certsVerif = certs.Count(c => c.Estado == EstadoCertificadoCv.Verificado);
            var porTipo = certs.GroupBy(c => c.Tipo.ToString()).Select(g => new CertificadoCvResumenDto(g.Key, g.Count())).ToList();
            var completitud = 0.0;
            if (!string.IsNullOrWhiteSpace(empleado.CvObservaciones)) completitud += 25;
            if (!string.IsNullOrWhiteSpace(empleado.CvTelefono)) completitud += 15;
            if (exps.Count > 0) completitud += 20;
            if (certs.Count > 0) completitud += 20;
            if (ants.Count > 0) completitud += 20;
            cvDto = new DashboardCvDto(exps.Count, ants.Count, certs.Count, certsVerif, completitud, porTipo);
        }
        catch { }

        // Certificados laborales
        DashboardCertificadosDto? certsDto = null;
        try
        {
            var certsLab = await _certificados.GetByEmpleadoAsync(empleado.Id, ct);
            certsDto = new DashboardCertificadosDto(certsLab.Count, certsLab.Count(c => c.ArchivoAdjuntoId != null));
        }
        catch { }

        return new DashboardEmpleadoDto(ultimoRecibo, pendientes, aprobadas, rechazadas, fichadas, feed, noLeidas,
            recibosDto, licenciasDto, fichadasDetallado, cvDto, certsDto);
    }
}

public sealed record GetDashboardEmpleadorQuery(Guid EmpleadoId) : IRequest<DashboardEmpleadorDto>;

public sealed class GetDashboardEmpleadorQueryHandler : IRequestHandler<GetDashboardEmpleadorQuery,
    DashboardEmpleadorDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IRelacionACargoRepository _relaciones;
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IAprobacionRepository _aprobaciones;
    private readonly IAccesoLogRepository _accesos;
    private readonly IRelojDataSource _reloj;
    private readonly IAprobadorResolver _resolver;

    public GetDashboardEmpleadorQueryHandler(IEmpleadoRepository empleados, IRelacionACargoRepository relaciones,
        ISolicitudLicenciaRepository solicitudes, ITipoLicenciaRepository tipos, IAprobacionRepository aprobaciones,
        IAccesoLogRepository accesos, IRelojDataSource reloj, IAprobadorResolver resolver)
    {
        _empleados = empleados;
        _relaciones = relaciones;
        _solicitudes = solicitudes;
        _tipos = tipos;
        _aprobaciones = aprobaciones;
        _accesos = accesos;
        _reloj = reloj;
        _resolver = resolver;
    }

    public async Task<DashboardEmpleadorDto> Handle(GetDashboardEmpleadorQuery request, CancellationToken ct)
    {
        var yo = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var relacionesACargo = await _relaciones.GetVigentesDeResponsableAsync(yo.Id, ct);
        var idsACargo = relacionesACargo.Select(r => r.EmpleadoId).ToHashSet();

        var enEspera = await _solicitudes.GetEnEsperaAsync(ct);
        var pendientesMias = 0;
        foreach (var solicitud in enEspera)
        {
            if (await _resolver.PuedeActuarAsync(solicitud, yo, ct))
                pendientesMias++;
        }

        var esAdminORrhh = yo.TieneRol(Rol.Rrhh) || yo.TieneRol(Rol.Administrador) || yo.TieneRol(Rol.Direccion);

        // Licencias por tipo del mes
        var todas = await _solicitudes.GetAllAsync(ct);
        var tipos = (await _tipos.GetAllAsync(ct)).ToDictionary(t => t.Id);
        var mesActual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var delMes = todas.Where(s => s.FechaSolicitud >= mesActual && s.FechaSolicitud < mesActual.AddMonths(1))
            .ToList();
        var porTipo = delMes
            .GroupBy(s => s.TipoLicenciaId)
            .Select(g => new LicenciasPorTipoDto(tipos.GetValueOrDefault(g.Key)?.Nombre ?? "?", g.Count(),
                g.Sum(s => s.Dias)))
            .OrderByDescending(l => l.Cantidad)
            .ToList();

        // Tiempo promedio de aprobación (solicitudes aprobadas)
        TimeSpan? promedio = null;
        var aprobadas = todas.Where(s => s.Estado == EstadoSolicitud.Aprobada).ToList();
        if (aprobadas.Count > 0)
        {
            var duraciones = new List<TimeSpan>();
            foreach (var s in aprobadas)
            {
                var decisiones = await _aprobaciones.GetBySolicitudAsync(s.Id, ct);
                var primeraDecision = decisiones.OrderBy(d => d.FechaHora).FirstOrDefault();
                if (primeraDecision is not null)
                    duraciones.Add(primeraDecision.FechaHora - s.FechaSolicitud);
            }
            if (duraciones.Count > 0)
                promedio = TimeSpan.FromTicks((long)duraciones.Average(d => d.Ticks));
        }

        // Accesos de los últimos 7 días
        var desde7 = DateTime.UtcNow.AddDays(-7);
        var accesos7 = await _accesos.GetEntreAsync(desde7, DateTime.UtcNow, ct);

        // Asistencia del personal a cargo este mes (lectura en vivo del reloj)
        var asistencia = new List<AsistenciaPorEmpleadoDto>();
        if (idsACargo.Count > 0)
        {
            var hastaMes = mesActual.AddMonths(1).AddSeconds(-1);
            var aCargo = (await _empleados.GetAllAsync(ct)).Where(e => idsACargo.Contains(e.Id)).ToList();
            var legajosACargo = aCargo.Select(e => e.Legajo).ToHashSet();
            var marcas = await _reloj.ObtenerMarcasAsync(null, mesActual, hastaMes, ct);

            foreach (var empleado in aCargo)
            {
                var marcasDe = marcas.Where(m => m.Legajo == empleado.Legajo).ToList();
                var resumen = CalculadorJornadas.CalcularResumen(CalculadorJornadas.AgruparEnJornadas(marcasDe));
                asistencia.Add(new AsistenciaPorEmpleadoDto(empleado.Id, empleado.NombreCompleto, empleado.Legajo, null,
                    resumen.DiasTrabajados, resumen.DiasConAnomalia, resumen.TotalHoras));
            }
        }

        return new DashboardEmpleadorDto(
            pendientesMias,
            idsACargo.Count,
            esAdminORrhh ? enEspera.Count : pendientesMias,
            porTipo,
            promedio,
            AgregadorEstadisticas.PorSemana(accesos7),
            asistencia.OrderByDescending(a => a.TotalHoras).ToList());
    }
}