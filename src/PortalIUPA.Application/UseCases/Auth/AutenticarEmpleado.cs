using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Application.UseCases.Auth;

/// <summary>
/// Autentica un empleado tras el login de Google: valida dominio institucional, busca el empleado
/// por correo (clave del legacy), registra el acceso en la auditoría y emite el JWT propio.
/// </summary>
public sealed record AutenticarEmpleadoCommand(string Correo, string? Ip, string? Dispositivo)
    : IRequest<TokenResponse>;

public sealed class AutenticarEmpleadoCommandValidator : AbstractValidator<AutenticarEmpleadoCommand>
{
    public AutenticarEmpleadoCommandValidator()
    {
        RuleFor(c => c.Correo).NotEmpty().WithMessage("El correo es obligatorio.");
    }
}

public sealed class AutenticarEmpleadoCommandHandler : IRequestHandler<AutenticarEmpleadoCommand, TokenResponse>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IAccesoLogRepository _accesos;
    private readonly ITokenService _tokenService;

    public AutenticarEmpleadoCommandHandler(IEmpleadoRepository empleados, IAccesoLogRepository accesos,
        ITokenService tokenService)
    {
        _empleados = empleados;
        _accesos = accesos;
        _tokenService = tokenService;
    }

    public async Task<TokenResponse> Handle(AutenticarEmpleadoCommand request, CancellationToken ct)
    {
        Email email;
        try
        {
            email = new Email(request.Correo);
        }
        catch (ArgumentException)
        {
            throw new AccesoDenegadoException("El correo de Google no tiene un formato válido.");
        }

        if (!email.EsInstitucional)
        {
            await RegistrarAccesoAsync(email.Valor, "LoginDominioRechazado", request, null, ct);
            throw new AccesoDenegadoException(
                "Solo se permiten cuentas institucionales @iupa.edu.ar. Ingresá con tu correo del IUPA.");
        }

        var empleado = await _empleados.GetByCorreoAsync(email.Valor, ct);
        if (empleado is null || !empleado.Activo)
        {
            await RegistrarAccesoAsync(email.Valor, "LoginEmpleadoNoRegistrado", request, null, ct);
            throw new AccesoDenegadoException(
                "Su cuenta no está asociada a un empleado registrado en el portal. Comuníquese con RR.HH.");
        }

        await RegistrarAccesoAsync(email.Valor, "Login", request, empleado, ct);

        return new TokenResponse(
            _tokenService.GenerarToken(empleado),
            empleado.Id,
            empleado.Nombre,
            empleado.Apellido,
            empleado.Correo.Valor,
            empleado.Roles.ToList(),
            empleado.AreaId,
            empleado.Legajo);
    }

    private async Task RegistrarAccesoAsync(string correo, string accion, AutenticarEmpleadoCommand request,
        Empleado? empleado, CancellationToken ct)
    {
        await _accesos.AddAsync(new AccesoLog(correo, accion, request.Ip, request.Dispositivo, empleado?.Id), ct);
    }
}

/// <summary>Registro de acciones de acceso (logins fallidos, logout, etc.) desde la API.</summary>
public sealed record RegistrarAccesoCommand(string Correo, string Accion, string? Ip, string? Dispositivo,
    Guid? EmpleadoId = null) : IRequest<Unit>;

public sealed class RegistrarAccesoCommandHandler : IRequestHandler<RegistrarAccesoCommand, Unit>
{
    private readonly IAccesoLogRepository _accesos;

    public RegistrarAccesoCommandHandler(IAccesoLogRepository accesos) => _accesos = accesos;

    public async Task<Unit> Handle(RegistrarAccesoCommand request, CancellationToken ct)
    {
        await _accesos.AddAsync(new AccesoLog(request.Correo, request.Accion, request.Ip, request.Dispositivo,
            request.EmpleadoId), ct);
        return Unit.Value;
    }
}