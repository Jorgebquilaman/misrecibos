using System.Net.Http;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Recibos;

public sealed record GetRecibosDisponiblesQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<ReciboDisponibleDto>>;

public sealed class GetRecibosDisponiblesQueryHandler : IRequestHandler<GetRecibosDisponiblesQuery,
    IReadOnlyList<ReciboDisponibleDto>>
{
    private readonly IPeriodoRepository _periodos;
    private readonly IDescargaReciboRepository _descargas;

    public GetRecibosDisponiblesQueryHandler(IPeriodoRepository periodos, IDescargaReciboRepository descargas)
    {
        _periodos = periodos;
        _descargas = descargas;
    }

    public async Task<IReadOnlyList<ReciboDisponibleDto>> Handle(GetRecibosDisponiblesQuery request,
        CancellationToken ct)
    {
        var periodos = await _periodos.GetActivosAsync(ct);

        var resultado = new List<ReciboDisponibleDto>();
        foreach (var periodo in periodos.OrderByDescending(p => p.Codigo))
        {
            var ultima = await _descargas.GetUltimaDeEmpleadoEnPeriodoAsync(request.EmpleadoId, periodo.Id, ct);
            resultado.Add(new ReciboDisponibleDto(periodo.Id, periodo.Codigo, periodo.Descripcion,
                ultima is not null, ultima?.FechaHora));
        }

        return resultado;
    }
}

/// <summary>Descarga el PDF del recibo desde JasperReports Server y registra la descarga en la auditoría.</summary>
public sealed record DescargarReciboCommand(Guid EmpleadoId, Guid PeriodoId, OrigenDescarga Origen, string? Ip = null)
    : IRequest<ReciboPdfResult>;

public sealed class DescargarReciboCommandHandler : IRequestHandler<DescargarReciboCommand, ReciboPdfResult>
{
    private const string ReporteRecibo = "Mapuche/Reportes/Recibos_de_Sueldo_simple";

    private readonly IEmpleadoRepository _empleados;
    private readonly IPeriodoRepository _periodos;
    private readonly IDescargaReciboRepository _descargas;
    private readonly IJasperReportClient _jasper;

    public DescargarReciboCommandHandler(IEmpleadoRepository empleados, IPeriodoRepository periodos,
        IDescargaReciboRepository descargas, IJasperReportClient jasper)
    {
        _empleados = empleados;
        _periodos = periodos;
        _descargas = descargas;
        _jasper = jasper;
    }

    public async Task<ReciboPdfResult> Handle(DescargarReciboCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");
        var periodo = await _periodos.GetByIdAsync(request.PeriodoId, ct)
            ?? throw new EntidadNoEncontradaException("El período no existe.");
        if (!periodo.Activo)
            throw new ReglaDeNegocioException("El período de liquidación no está activo.");
        if (periodo.NroLiq is null)
            throw new ReglaDeNegocioException(
                $"El período {periodo.Codigo} no tiene liquidación asociada (NroLiq). Contacte a administración.");

        var parametros = new Dictionary<string, string>
        {
            ["nroliq"] = periodo.NroLiq.Value.ToString(),
            ["nroleg_f"] = empleado.Legajo.ToString(),
            ["nroleg_i"] = empleado.Legajo.ToString(),
        };

        byte[] pdf;
        try
        {
            pdf = await _jasper.GetPdfAsync(ReporteRecibo, parametros, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ReglaDeNegocioException(
                "El servidor de recibos tardó demasiado en responder. Volvé a intentar en unos minutos.");
        }
        catch (HttpRequestException)
        {
            throw new ReglaDeNegocioException(
                $"No se pudo generar el recibo del período {periodo.Codigo}. Si el problema persiste, contacte a administración.");
        }

        await _descargas.AddAsync(new DescargaRecibo(empleado.Id, periodo.Id, request.Origen, request.Ip), ct);

        return new ReciboPdfResult(pdf, $"Recibo_{periodo.Codigo}_{empleado.Legajo}.pdf", empleado.Legajo);
    }
}

/// <summary>Envía el recibo por correo institucional (opción secundaria, útil en desktop sin descarga).</summary>
public sealed record EnviarReciboPorEmailCommand(Guid EmpleadoId, Guid PeriodoId) : IRequest<Unit>;

public sealed class EnviarReciboPorEmailCommandHandler : IRequestHandler<EnviarReciboPorEmailCommand, Unit>
{
    private readonly IMediator _mediator;
    private readonly IEmpleadoRepository _empleados;
    private readonly IPeriodoRepository _periodos;
    private readonly IEmailPort _email;
    private readonly IDescargaReciboRepository _descargas;

    public EnviarReciboPorEmailCommandHandler(IMediator mediator, IEmpleadoRepository empleados,
        IPeriodoRepository periodos, IEmailPort email, IDescargaReciboRepository descargas)
    {
        _mediator = mediator;
        _empleados = empleados;
        _periodos = periodos;
        _email = email;
        _descargas = descargas;
    }

    public async Task<Unit> Handle(EnviarReciboPorEmailCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");
        var periodo = await _periodos.GetByIdAsync(request.PeriodoId, ct)
            ?? throw new EntidadNoEncontradaException("El período no existe.");

        var pdf = await _mediator.Send(new DescargarReciboCommand(request.EmpleadoId, request.PeriodoId,
            OrigenDescarga.Email), ct);

        await _email.EnviarAsync(
            empleado.Correo.Valor,
            $"IUPA – Recibo de sueldo {periodo.Codigo}",
            $"Estimado/a {empleado.Nombre}, adjuntamos su recibo de sueldo correspondiente al período <b>{periodo.Codigo}</b>.",
            new[] { new EmailAdjunto(pdf.NombreArchivo, pdf.Pdf, "application/pdf") },
            ct);

        await _descargas.AddAsync(new DescargaRecibo(empleado.Id, periodo.Id, OrigenDescarga.Email), ct);

        return Unit.Value;
    }
}

public sealed record GetHistorialDescargasQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<DescargaReciboDto>>;

public sealed class GetHistorialDescargasQueryHandler : IRequestHandler<GetHistorialDescargasQuery,
    IReadOnlyList<DescargaReciboDto>>
{
    private readonly IDescargaReciboRepository _descargas;
    private readonly IPeriodoRepository _periodos;

    public GetHistorialDescargasQueryHandler(IDescargaReciboRepository descargas, IPeriodoRepository periodos)
    {
        _descargas = descargas;
        _periodos = periodos;
    }

    public async Task<IReadOnlyList<DescargaReciboDto>> Handle(GetHistorialDescargasQuery request,
        CancellationToken ct)
    {
        var descargas = await _descargas.GetByEmpleadoAsync(request.EmpleadoId, ct);
        var periodos = await _periodos.GetAllAsync(ct);
        var porId = periodos.ToDictionary(p => p.Id);

        return descargas
            .OrderByDescending(d => d.FechaHora)
            .Select(d => new DescargaReciboDto(d.Id, d.PeriodoId,
                porId.TryGetValue(d.PeriodoId, out var p) ? p.Codigo : "?", d.FechaHora, d.Origen))
            .ToList();
    }
}