using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Application.DTOs;

public sealed record CertificadoDto(
    Guid Id,
    TipoCertificado Tipo,
    DateOnly Desde,
    DateOnly Hasta,
    string? Destino,
    EstadoCertificado Estado,
    Guid? ArchivoAdjuntoId,
    DateTime FechaSolicitud);

public sealed record CertificadoPdfResult(byte[] Pdf, string NombreArchivo);