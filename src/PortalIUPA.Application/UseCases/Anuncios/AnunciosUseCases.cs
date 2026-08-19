using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Anuncios;

public sealed record GetFeedAnunciosQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<AnuncioDto>>;

public sealed class GetFeedAnunciosQueryHandler : IRequestHandler<GetFeedAnunciosQuery, IReadOnlyList<AnuncioDto>>
{
    private readonly IAnuncioRepository _anuncios;
    private readonly IAnuncioLeidoRepository _leidos;
    private readonly IEmpleadoRepository _empleados;

    public GetFeedAnunciosQueryHandler(IAnuncioRepository anuncios, IAnuncioLeidoRepository leidos,
        IEmpleadoRepository empleados)
    {
        _anuncios = anuncios;
        _leidos = leidos;
        _empleados = empleados;
    }

    public async Task<IReadOnlyList<AnuncioDto>> Handle(GetFeedAnunciosQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var vigentes = await _anuncios.GetVigentesParaAsync(empleado, DateOnly.FromDateTime(DateTime.Now), ct);
        var leidos = await _leidos.GetDeEmpleadoAsync(empleado.Id, ct);
        var leidosIds = leidos.Select(l => l.AnuncioId).ToHashSet();

        return vigentes
            .OrderByDescending(a => a.Prioridad)
            .ThenByDescending(a => a.FechaCreacion)
            .Select(a => new AnuncioDto(a.Id, a.Titulo, a.Cuerpo, a.FechaDesde, a.FechaHasta, a.Prioridad, a.Tipo,
                a.Alcance, a.AreaId, a.Rol, a.Activo, leidosIds.Contains(a.Id), a.FechaCreacion))
            .ToList();
    }
}

public sealed record MarcarAnuncioLeidoCommand(Guid AnuncioId, Guid EmpleadoId) : IRequest<Unit>;

public sealed class MarcarAnuncioLeidoCommandHandler : IRequestHandler<MarcarAnuncioLeidoCommand, Unit>
{
    private readonly IAnuncioLeidoRepository _leidos;
    private readonly IAnuncioRepository _anuncios;

    public MarcarAnuncioLeidoCommandHandler(IAnuncioLeidoRepository leidos, IAnuncioRepository anuncios)
    {
        _leidos = leidos;
        _anuncios = anuncios;
    }

    public async Task<Unit> Handle(MarcarAnuncioLeidoCommand request, CancellationToken ct)
    {
        var anuncio = await _anuncios.GetByIdAsync(request.AnuncioId, ct)
            ?? throw new EntidadNoEncontradaException("El anuncio no existe.");
        if (!await _leidos.FueLeidoAsync(anuncio.Id, request.EmpleadoId, ct))
            await _leidos.AddAsync(new AnuncioLeido(anuncio.Id, request.EmpleadoId), ct);

        return Unit.Value;
    }
}

public sealed record CreateAnuncioCommand(string Titulo, string Cuerpo, Guid CreadoPor,
    PrioridadAnuncio Prioridad, TipoAnuncio Tipo, DateOnly? FechaDesde, DateOnly? FechaHasta,
    AlcanceAnuncio Alcance, Guid? AreaId, Rol? Rol) : IRequest<AnuncioDto>;

public sealed class CreateAnuncioCommandValidator : AbstractValidator<CreateAnuncioCommand>
{
    public CreateAnuncioCommandValidator()
    {
        RuleFor(c => c.Titulo).NotEmpty().MaximumLength(200).WithMessage("El título es obligatorio (máx. 200).");
        RuleFor(c => c.Cuerpo).NotEmpty().WithMessage("El cuerpo es obligatorio.");
        RuleFor(c => c.FechaHasta).GreaterThanOrEqualTo(c => c.FechaDesde)
            .WithMessage("La fecha de fin no puede ser anterior a la de inicio.");
        RuleFor(c => c).Must(c => c.Alcance != AlcanceAnuncio.Area || c.AreaId is not null)
            .WithMessage("El alcance por área requiere seleccionar un área.");
        RuleFor(c => c).Must(c => c.Alcance != AlcanceAnuncio.Rol || c.Rol is not null)
            .WithMessage("El alcance por rol requiere seleccionar un rol.");
    }
}

public sealed class CreateAnuncioCommandHandler : IRequestHandler<CreateAnuncioCommand, AnuncioDto>
{
    private readonly IAnuncioRepository _anuncios;

    public CreateAnuncioCommandHandler(IAnuncioRepository anuncios) => _anuncios = anuncios;

    public async Task<AnuncioDto> Handle(CreateAnuncioCommand request, CancellationToken ct)
    {
        var anuncio = new Anuncio(request.Titulo, request.Cuerpo, request.CreadoPor, request.Prioridad, request.Tipo,
            request.FechaDesde, request.FechaHasta, request.Alcance, request.AreaId, request.Rol);
        await _anuncios.AddAsync(anuncio, ct);

        return new AnuncioDto(anuncio.Id, anuncio.Titulo, anuncio.Cuerpo, anuncio.FechaDesde, anuncio.FechaHasta,
            anuncio.Prioridad, anuncio.Tipo, anuncio.Alcance, anuncio.AreaId, anuncio.Rol, anuncio.Activo, false,
            anuncio.FechaCreacion);
    }
}

public sealed record UpdateAnuncioCommand(Guid Id, string Titulo, string Cuerpo, PrioridadAnuncio Prioridad,
    TipoAnuncio Tipo, DateOnly? FechaDesde, DateOnly? FechaHasta, AlcanceAnuncio Alcance, Guid? AreaId, Rol? Rol)
    : IRequest<AnuncioDto>;

public sealed class UpdateAnuncioCommandHandler : IRequestHandler<UpdateAnuncioCommand, AnuncioDto>
{
    private readonly IAnuncioRepository _anuncios;

    public UpdateAnuncioCommandHandler(IAnuncioRepository anuncios) => _anuncios = anuncios;

    public async Task<AnuncioDto> Handle(UpdateAnuncioCommand request, CancellationToken ct)
    {
        var anuncio = await _anuncios.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El anuncio no existe.");

        anuncio.Actualizar(request.Titulo, request.Cuerpo, request.Prioridad, request.Tipo, request.FechaDesde,
            request.FechaHasta, request.Alcance, request.AreaId, request.Rol);
        await _anuncios.UpdateAsync(anuncio, ct);

        return new AnuncioDto(anuncio.Id, anuncio.Titulo, anuncio.Cuerpo, anuncio.FechaDesde, anuncio.FechaHasta,
            anuncio.Prioridad, anuncio.Tipo, anuncio.Alcance, anuncio.AreaId, anuncio.Rol, anuncio.Activo, false,
            anuncio.FechaCreacion);
    }
}

public sealed record SetActivoAnuncioCommand(Guid Id, bool Activo) : IRequest<Unit>;

public sealed class SetActivoAnuncioCommandHandler : IRequestHandler<SetActivoAnuncioCommand, Unit>
{
    private readonly IAnuncioRepository _anuncios;

    public SetActivoAnuncioCommandHandler(IAnuncioRepository anuncios) => _anuncios = anuncios;

    public async Task<Unit> Handle(SetActivoAnuncioCommand request, CancellationToken ct)
    {
        var anuncio = await _anuncios.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El anuncio no existe.");

        if (request.Activo) anuncio.Activar();
        else anuncio.Desactivar();

        await _anuncios.UpdateAsync(anuncio, ct);
        return Unit.Value;
    }
}

public sealed record GetAnunciosAdminQuery : IRequest<IReadOnlyList<AnuncioDto>>;

public sealed class GetAnunciosAdminQueryHandler : IRequestHandler<GetAnunciosAdminQuery, IReadOnlyList<AnuncioDto>>
{
    private readonly IAnuncioRepository _anuncios;

    public GetAnunciosAdminQueryHandler(IAnuncioRepository anuncios) => _anuncios = anuncios;

    public async Task<IReadOnlyList<AnuncioDto>> Handle(GetAnunciosAdminQuery request, CancellationToken ct)
    {
        var anuncios = await _anuncios.GetAllAsync(ct);
        return anuncios
            .OrderByDescending(a => a.FechaCreacion)
            .Select(a => new AnuncioDto(a.Id, a.Titulo, a.Cuerpo, a.FechaDesde, a.FechaHasta, a.Prioridad, a.Tipo,
                a.Alcance, a.AreaId, a.Rol, a.Activo, false, a.FechaCreacion))
            .ToList();
    }
}