using MediatR;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Estadisticas;

public sealed record GetEstadisticasGeneralesQuery : IRequest<EstadisticasGeneralesDto>;

public sealed record EstadisticasGeneralesDto(
    EmpleadosEstadisticasDto Empleados,
    LicenciasEstadisticasDto Licencias,
    RecibosEstadisticasDto Recibos,
    CvEstadisticasDto Cv,
    CertificadosEstadisticasDto Certificados,
    AnunciosEstadisticasDto Anuncios
);

public sealed record EmpleadosEstadisticasDto(int Total, int Activos, int Inactivos, IReadOnlyList<ConteoDto> PorArea, IReadOnlyList<ConteoDto> PorRol, IReadOnlyList<ConteoDto> PorEstadoCv);
public sealed record LicenciasEstadisticasDto(int Total, IReadOnlyList<ConteoDto> PorEstado, IReadOnlyList<ConteoDto> PorTipo, IReadOnlyList<MesConteoDto> Ultimos6Meses);
public sealed record RecibosEstadisticasDto(int TotalPeriodos, int PeriodosActivos, int TotalDescargas, IReadOnlyList<ConteoDto> PorPeriodo, IReadOnlyList<MesConteoDto> PorMes);
public sealed record CvEstadisticasDto(int TotalExperiencias, int TotalAntecedentes, int TotalCertificados, IReadOnlyList<ConteoDto> CertificadosPorTipo, IReadOnlyList<ConteoDto> CertificadosPorEstado, double CompletitudPromedio, IReadOnlyList<ConteoDto> CompletitudRangos);
public sealed record CertificadosEstadisticasDto(int Total, IReadOnlyList<ConteoDto> PorTipo);
public sealed record AnunciosEstadisticasDto(int Total, IReadOnlyList<ConteoDto> PorTipo, IReadOnlyList<ConteoDto> PorPrioridad, IReadOnlyList<ConteoDto> PorAlcance);
public sealed record ConteoDto(string Nombre, int Cantidad);
public sealed record MesConteoDto(string Mes, int Cantidad);

public sealed class GetEstadisticasGeneralesQueryHandler : IRequestHandler<GetEstadisticasGeneralesQuery, EstadisticasGeneralesDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IPeriodoRepository _periodos;
    private readonly IDescargaReciboRepository _descargas;
    private readonly ICertificadoLaboralRepository _certificados;
    private readonly ICertificadoCvRepository _certificadosCv;
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly IAnuncioRepository _anuncios;

    public GetEstadisticasGeneralesQueryHandler(IEmpleadoRepository empleados, IAreaRepository areas,
        ISolicitudLicenciaRepository solicitudes, ITipoLicenciaRepository tipos,
        IPeriodoRepository periodos, IDescargaReciboRepository descargas,
        ICertificadoLaboralRepository certificados, ICertificadoCvRepository certificadosCv,
        ICvExperienciaRepository experiencias, ICvAntecedenteAcademicoRepository antecedentes,
        IAnuncioRepository anuncios)
    {
        _empleados = empleados;
        _areas = areas;
        _solicitudes = solicitudes;
        _tipos = tipos;
        _periodos = periodos;
        _descargas = descargas;
        _certificados = certificados;
        _certificadosCv = certificadosCv;
        _experiencias = experiencias;
        _antecedentes = antecedentes;
        _anuncios = anuncios;
    }

    public async Task<EstadisticasGeneralesDto> Handle(GetEstadisticasGeneralesQuery request, CancellationToken ct)
    {
        var empleados = await _empleados.GetAllAsync(ct);
        var areas = await _areas.GetAllAsync(ct);
        var areaMap = areas.ToDictionary(a => a.Id, a => a.Nombre);

        // Empleados
        var total = empleados.Count;
        var activos = empleados.Count(e => e.Activo);
        var porArea = empleados.Where(e => e.AreaId.HasValue).GroupBy(e => areaMap.GetValueOrDefault(e.AreaId!.Value, "Sin área"))
            .Select(g => new ConteoDto(g.Key, g.Count())).OrderByDescending(c => c.Cantidad).Take(8).ToList();
        if (empleados.Any(e => e.AreaId == null)) porArea.Add(new ConteoDto("Sin área", empleados.Count(e => e.AreaId == null)));
        var porRol = empleados.SelectMany(e => e.Roles).GroupBy(r => r.ToString())
            .Select(g => new ConteoDto(g.Key, g.Count())).OrderByDescending(c => c.Cantidad).ToList();
        var porEstadoCv = new List<ConteoDto>
        {
            new("Con observaciones", empleados.Count(e => !string.IsNullOrWhiteSpace(e.CvObservaciones))),
            new("Sin observaciones", empleados.Count(e => string.IsNullOrWhiteSpace(e.CvObservaciones)))
        };

        // Licencias
        var solicitudes = await _solicitudes.GetAllAsync(ct);
        var totalLic = solicitudes.Count;
        var porEstado = solicitudes.GroupBy(s => s.Estado.ToString()).Select(g => new ConteoDto(g.Key, g.Count())).ToList();
        var tipos = (await _tipos.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.Nombre);
        var porTipo = solicitudes.GroupBy(s => tipos.GetValueOrDefault(s.TipoLicenciaId, "Desconocido"))
            .Select(g => new ConteoDto(g.Key, g.Count())).OrderByDescending(c => c.Cantidad).Take(6).ToList();
        var ultimos6Meses = Enumerable.Range(0, 6).Select(i =>
        {
            var d = DateTime.Today.AddMonths(-i);
            var mesStr = d.ToString("MM/yy");
            var c = solicitudes.Count(s => s.FechaSolicitud.Year == d.Year && s.FechaSolicitud.Month == d.Month);
            return new MesConteoDto(mesStr, c);
        }).Reverse().ToList();

        // Recibos
        var periodos = await _periodos.GetAllAsync(ct);
        var periodosActivos = periodos.Count(p => p.Activo);
        var totalDescargas = 0;
        var descargasPorPeriodo = new List<ConteoDto>();
        var descargasPorMes = new Dictionary<string, int>();
        foreach (var p in periodos.OrderByDescending(p => p.Codigo).Take(10))
        {
            var count = 0;
            foreach (var e in empleados)
            {
                var list = await _descargas.GetByEmpleadoAsync(e.Id, ct);
                count += list.Count(d => d.PeriodoId == p.Id);
                foreach (var d in list.Where(d => d.PeriodoId == p.Id))
                {
                    var mes = d.FechaHora.ToString("MM/yy");
                    descargasPorMes[mes] = descargasPorMes.GetValueOrDefault(mes) + 1;
                }
            }
            totalDescargas += count;
            if (count > 0) descargasPorPeriodo.Add(new ConteoDto(p.Codigo, count));
        }
        // Si no hay por periodo, calcular total general
        if (totalDescargas == 0)
        {
            foreach (var e in empleados)
            {
                var list = await _descargas.GetByEmpleadoAsync(e.Id, ct);
                totalDescargas += list.Count;
                foreach (var d in list)
                {
                    var mes = d.FechaHora.ToString("MM/yy");
                    descargasPorMes[mes] = descargasPorMes.GetValueOrDefault(mes) + 1;
                }
            }
        }
        var porMes = descargasPorMes.OrderBy(k => DateTime.ParseExact(k.Key, "MM/yy", null)).Select(k => new MesConteoDto(k.Key, k.Value)).TakeLast(6).ToList();

        // CV
        var totalExps = 0; var totalAnts = 0; var totalCertsCv = 0;
        var certsPorTipo = new Dictionary<string, int>();
        var certsPorEstado = new Dictionary<string, int>();
        var completitudes = new List<double>();
        foreach (var e in empleados)
        {
            var exps = await _experiencias.GetByEmpleadoAsync(e.Id, ct);
            var ants = await _antecedentes.GetByEmpleadoAsync(e.Id, ct);
            var certs = await _certificadosCv.GetByEmpleadoAsync(e.Id, ct);
            totalExps += exps.Count;
            totalAnts += ants.Count;
            totalCertsCv += certs.Count;
            foreach (var c in certs)
            {
                var t = c.Tipo.ToString();
                certsPorTipo[t] = certsPorTipo.GetValueOrDefault(t) + 1;
                var es = c.Estado.ToString();
                certsPorEstado[es] = certsPorEstado.GetValueOrDefault(es) + 1;
            }
            var comp = 0.0;
            if (!string.IsNullOrWhiteSpace(e.CvObservaciones)) comp += 25;
            if (!string.IsNullOrWhiteSpace(e.CvTelefono)) comp += 15;
            if (exps.Count > 0) comp += 20;
            if (certs.Count > 0) comp += 20;
            if (ants.Count > 0) comp += 20;
            completitudes.Add(comp);
        }
        var compProm = completitudes.Count > 0 ? completitudes.Average() : 0;
        var compRangos = new[]
        {
            new ConteoDto("0-25%", completitudes.Count(c => c <= 25)),
            new ConteoDto("26-50%", completitudes.Count(c => c > 25 && c <= 50)),
            new ConteoDto("51-75%", completitudes.Count(c => c > 50 && c <= 75)),
            new ConteoDto("76-100%", completitudes.Count(c => c > 75)),
        }.Where(c => c.Cantidad > 0).ToList();

        // Certificados laborales
        var certsLabTotal = 0;
        var porTipoLab = new Dictionary<string, int>();
        foreach (var e in empleados)
        {
            var list = await _certificados.GetByEmpleadoAsync(e.Id, ct);
            certsLabTotal += list.Count;
            foreach (var c in list)
            {
                var t = c.Tipo.ToString();
                porTipoLab[t] = porTipoLab.GetValueOrDefault(t) + 1;
            }
        }

        // Anuncios
        var anuncios = await _anuncios.GetAllAsync(ct);
        var anunPorTipo = anuncios.GroupBy(a => a.Tipo.ToString()).Select(g => new ConteoDto(g.Key, g.Count())).ToList();
        var anunPorPrioridad = anuncios.GroupBy(a => a.Prioridad.ToString()).Select(g => new ConteoDto(g.Key, g.Count())).ToList();
        var anunPorAlcance = anuncios.GroupBy(a => a.Alcance.ToString()).Select(g => new ConteoDto(g.Key, g.Count())).ToList();

        return new EstadisticasGeneralesDto(
            new EmpleadosEstadisticasDto(total, activos, total - activos, porArea, porRol, porEstadoCv),
            new LicenciasEstadisticasDto(totalLic, porEstado, porTipo, ultimos6Meses),
            new RecibosEstadisticasDto(periodos.Count, periodosActivos, totalDescargas, descargasPorPeriodo, porMes),
            new CvEstadisticasDto(totalExps, totalAnts, totalCertsCv, certsPorTipo.Select(k => new ConteoDto(k.Key, k.Value)).ToList(), certsPorEstado.Select(k => new ConteoDto(k.Key, k.Value)).ToList(), compProm, compRangos),
            new CertificadosEstadisticasDto(certsLabTotal, porTipoLab.Select(k => new ConteoDto(k.Key, k.Value)).ToList()),
            new AnunciosEstadisticasDto(anuncios.Count, anunPorTipo, anunPorPrioridad, anunPorAlcance)
        );
    }
}


