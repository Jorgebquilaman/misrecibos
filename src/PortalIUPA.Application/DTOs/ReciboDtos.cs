using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Application.DTOs;

public sealed record TokenResponse(
    string Token,
    Guid EmpleadoId,
    string Nombre,
    string Apellido,
    string Correo,
    IReadOnlyList<Rol> Roles,
    Guid? AreaId,
    int Legajo);

public sealed record PeriodoDto(Guid Id, string Codigo, string? Descripcion, bool Activo, int? NroLiq);

public sealed record ReciboDisponibleDto(
    Guid PeriodoId,
    string PeriodoCodigo,
    string? PeriodoDescripcion,
    bool YaDescargado,
    DateTime? FechaUltimaDescarga);

public sealed record DescargaReciboDto(
    Guid Id,
    Guid PeriodoId,
    string PeriodoCodigo,
    DateTime FechaHora,
    OrigenDescarga Origen);

public sealed record ReciboPdfResult(byte[] Pdf, string NombreArchivo);