using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Application.UseCases.Certificados;

public sealed record GetMisCertificadosQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<CertificadoDto>>;

public sealed class GetMisCertificadosQueryHandler : IRequestHandler<GetMisCertificadosQuery,
    IReadOnlyList<CertificadoDto>>
{
    private readonly ICertificadoLaboralRepository _certificados;

    public GetMisCertificadosQueryHandler(ICertificadoLaboralRepository certificados) => _certificados = certificados;

    public async Task<IReadOnlyList<CertificadoDto>> Handle(GetMisCertificadosQuery request, CancellationToken ct)
    {
        var certificados = await _certificados.GetByEmpleadoAsync(request.EmpleadoId, ct);
        return certificados
            .OrderByDescending(c => c.FechaSolicitud)
            .Select(c => new CertificadoDto(c.Id, c.Tipo, c.Desde, c.Hasta, c.Destino, c.Estado,
                c.ArchivoAdjuntoId, c.FechaSolicitud))
            .ToList();
    }
}

/// <summary>Solicita y genera automáticamente el PDF del certificado laboral (constancia/certificación).</summary>
public sealed record SolicitarCertificadoCommand(Guid EmpleadoId, TipoCertificado Tipo, DateOnly Desde, DateOnly Hasta,
    string? Destino) : IRequest<CertificadoDto>;

public sealed class SolicitarCertificadoCommandValidator : AbstractValidator<SolicitarCertificadoCommand>
{
    public SolicitarCertificadoCommandValidator()
    {
        RuleFor(c => c.Hasta).GreaterThanOrEqualTo(c => c.Desde)
            .WithMessage("La fecha de fin no puede ser anterior a la de inicio.");
        RuleFor(c => c.Destino).MaximumLength(200).WithMessage("El destino es demasiado largo.");
    }
}

public sealed class SolicitarCertificadoCommandHandler : IRequestHandler<SolicitarCertificadoCommand, CertificadoDto>
{
    private readonly ICertificadoLaboralRepository _certificados;
    private readonly IEmpleadoRepository _empleados;
    private readonly IGeneradorPdfCertificado _generador;
    private readonly IFileStoragePort _storage;
    private readonly IAdjuntoRepository _adjuntos;

    public SolicitarCertificadoCommandHandler(ICertificadoLaboralRepository certificados, IEmpleadoRepository empleados,
        IGeneradorPdfCertificado generador, IFileStoragePort storage, IAdjuntoRepository adjuntos)
    {
        _certificados = certificados;
        _empleados = empleados;
        _generador = generador;
        _storage = storage;
        _adjuntos = adjuntos;
    }

    public async Task<CertificadoDto> Handle(SolicitarCertificadoCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var certificado = new CertificadoLaboral(empleado.Id, request.Tipo,
            new RangoFechas(request.Desde, request.Hasta), request.Destino);
        await _certificados.AddAsync(certificado, ct);

        var datos = new DatosCertificadoPdf(request.Tipo, empleado.NombreCompleto, empleado.Legajo, empleado.Dni,
            empleado.Cuil, request.Desde, request.Hasta, request.Destino);
        var pdf = await _generador.GenerarAsync(datos, ct);

        var storageKey = $"certificados/{certificado.Id}.pdf";
        await using var stream = new MemoryStream(pdf);
        await _storage.GuardarAsync(storageKey, "application/pdf", stream, ct);

        var nombreArchivo = $"{request.Tipo switch
        {
            TipoCertificado.ConstanciaTrabajo => "Constancia_de_trabajo",
            _ => "Certificacion_de_servicios",
        }}_{empleado.Legajo}.pdf";

        var adjunto = new Adjunto(nombreArchivo, "application/pdf", pdf.Length, storageKey, empleado.Id);
        adjunto.VincularAEntidad("CertificadoLaboral", certificado.Id);
        await _adjuntos.AddAsync(adjunto, ct);

        certificado.MarcarGenerado(adjunto.Id);
        await _certificados.UpdateAsync(certificado, ct);

        return new CertificadoDto(certificado.Id, certificado.Tipo, certificado.Desde, certificado.Hasta,
            certificado.Destino, certificado.Estado, certificado.ArchivoAdjuntoId, certificado.FechaSolicitud);
    }
}

/// <summary>Descarga el PDF del certificado (solo el dueño o administradores).</summary>
public sealed record DescargarCertificadoCommand(Guid CertificadoId, Guid UsuarioId) : IRequest<CertificadoPdfResult>;

public sealed class DescargarCertificadoCommandHandler : IRequestHandler<DescargarCertificadoCommand,
    CertificadoPdfResult>
{
    private readonly ICertificadoLaboralRepository _certificados;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;
    private readonly IEmpleadoRepository _empleados;

    public DescargarCertificadoCommandHandler(ICertificadoLaboralRepository certificados, IAdjuntoRepository adjuntos,
        IFileStoragePort storage, IEmpleadoRepository empleados)
    {
        _certificados = certificados;
        _adjuntos = adjuntos;
        _storage = storage;
        _empleados = empleados;
    }

    public async Task<CertificadoPdfResult> Handle(DescargarCertificadoCommand request, CancellationToken ct)
    {
        var certificado = await _certificados.GetByIdAsync(request.CertificadoId, ct)
            ?? throw new EntidadNoEncontradaException("El certificado no existe.");

        var usuario = await _empleados.GetByIdAsync(request.UsuarioId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        if (certificado.EmpleadoId != request.UsuarioId && !usuario.EsAdministrador && !usuario.TieneRol(Rol.Rrhh))
            throw new AccesoDenegadoException("Solo el titular del certificado puede descargarlo.");

        var adjunto = certificado.ArchivoAdjuntoId is not null
            ? await _adjuntos.GetByIdAsync(certificado.ArchivoAdjuntoId.Value, ct)
            : null;
        if (adjunto is null)
            throw new ReglaDeNegocioException("El certificado aún no tiene el archivo generado.");

        await using var stream = await _storage.AbrirAsync(adjunto.StorageKey, ct);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);

        return new CertificadoPdfResult(memory.ToArray(), adjunto.NombreArchivo);
    }
}