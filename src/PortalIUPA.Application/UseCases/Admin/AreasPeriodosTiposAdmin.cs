using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Admin;

public sealed record GetAreasQuery : IRequest<IReadOnlyList<AreaDto>>;

public sealed class GetAreasQueryHandler : IRequestHandler<GetAreasQuery, IReadOnlyList<AreaDto>>
{
    private readonly IAreaRepository _areas;

    public GetAreasQueryHandler(IAreaRepository areas) => _areas = areas;

    public async Task<IReadOnlyList<AreaDto>> Handle(GetAreasQuery request, CancellationToken ct)
    {
        var areas = await _areas.GetAllAsync(ct);
        return areas.Select(a => new AreaDto(a.Id, a.Nombre, a.Codigo, a.AreaPadreId, a.Activa)).ToList();
    }
}

public sealed record CreateAreaCommand(string Nombre, string Codigo, Guid? AreaPadreId) : IRequest<AreaDto>;

public sealed class CreateAreaCommandValidator : AbstractValidator<CreateAreaCommand>
{
    public CreateAreaCommandValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.");
        RuleFor(c => c.Codigo).NotEmpty().MaximumLength(10).WithMessage("El código es obligatorio (máx. 10).");
    }
}

public sealed class CreateAreaCommandHandler : IRequestHandler<CreateAreaCommand, AreaDto>
{
    private readonly IAreaRepository _areas;

    public CreateAreaCommandHandler(IAreaRepository areas) => _areas = areas;

    public async Task<AreaDto> Handle(CreateAreaCommand request, CancellationToken ct)
    {
        var area = new Area(request.Nombre, request.Codigo, request.AreaPadreId);
        await _areas.AddAsync(area, ct);
        return new AreaDto(area.Id, area.Nombre, area.Codigo, area.AreaPadreId, area.Activa);
    }
}

public sealed record UpdateAreaCommand(Guid Id, string Nombre, string Codigo, Guid? AreaPadreId, bool? Activa)
    : IRequest<AreaDto>;

public sealed class UpdateAreaCommandValidator : AbstractValidator<UpdateAreaCommand>
{
    public UpdateAreaCommandValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.");
        RuleFor(c => c.Codigo).NotEmpty().MaximumLength(10).WithMessage("El código es obligatorio (máx. 10).");
    }
}

public sealed class UpdateAreaCommandHandler : IRequestHandler<UpdateAreaCommand, AreaDto>
{
    private readonly IAreaRepository _areas;

    public UpdateAreaCommandHandler(IAreaRepository areas) => _areas = areas;

    public async Task<AreaDto> Handle(UpdateAreaCommand request, CancellationToken ct)
    {
        var area = await _areas.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El área no existe.");

        if (request.AreaPadreId == area.Id)
            throw new ReglaDeNegocioException("Un área no puede ser padre de sí misma.");

        area.Renombrar(request.Nombre);
        area.CambiarCodigo(request.Codigo);
        area.ReasignarPadre(request.AreaPadreId);
        if (request.Activa is true) area.Activar();
        if (request.Activa is false) area.Desactivar();
        await _areas.UpdateAsync(area, ct);

        return new AreaDto(area.Id, area.Nombre, area.Codigo, area.AreaPadreId, area.Activa);
    }
}

public sealed record GetPeriodosQuery : IRequest<IReadOnlyList<PeriodoDto>>;

public sealed class GetPeriodosQueryHandler : IRequestHandler<GetPeriodosQuery, IReadOnlyList<PeriodoDto>>
{
    private readonly IPeriodoRepository _periodos;

    public GetPeriodosQueryHandler(IPeriodoRepository periodos) => _periodos = periodos;

    public async Task<IReadOnlyList<PeriodoDto>> Handle(GetPeriodosQuery request, CancellationToken ct)
    {
        var periodos = await _periodos.GetAllAsync(ct);
        return periodos.OrderByDescending(p => p.Codigo)
            .Select(p => new PeriodoDto(p.Id, p.Codigo, p.Descripcion, p.Activo, p.NroLiq))
            .ToList();
    }
}

public sealed record CreatePeriodoCommand(string Codigo, string? Descripcion, int? NroLiq) : IRequest<PeriodoDto>;

public sealed class CreatePeriodoCommandValidator : AbstractValidator<CreatePeriodoCommand>
{
    public CreatePeriodoCommandValidator()
    {
        RuleFor(c => c.Codigo).NotEmpty().WithMessage("El código del período es obligatorio (ej. 2025-06).");
        RuleFor(c => c.NroLiq).GreaterThan(0).When(c => c.NroLiq.HasValue)
            .WithMessage("El número de liquidación debe ser positivo.");
    }
}

public sealed class CreatePeriodoCommandHandler : IRequestHandler<CreatePeriodoCommand, PeriodoDto>
{
    private readonly IPeriodoRepository _periodos;

    public CreatePeriodoCommandHandler(IPeriodoRepository periodos) => _periodos = periodos;

    public async Task<PeriodoDto> Handle(CreatePeriodoCommand request, CancellationToken ct)
    {
        if (await _periodos.GetByCodigoAsync(request.Codigo, ct) is not null)
            throw new ReglaDeNegocioException($"El período {request.Codigo} ya existe.");

        var periodo = new Periodo(request.Codigo, request.Descripcion, request.NroLiq);
        await _periodos.AddAsync(periodo, ct);
        return new PeriodoDto(periodo.Id, periodo.Codigo, periodo.Descripcion, periodo.Activo, periodo.NroLiq);
    }
}

public sealed record UpdatePeriodoCommand(Guid Id, string? Descripcion, bool Activo, int? NroLiq) : IRequest<PeriodoDto>;

public sealed class UpdatePeriodoCommandHandler : IRequestHandler<UpdatePeriodoCommand, PeriodoDto>
{
    private readonly IPeriodoRepository _periodos;

    public UpdatePeriodoCommandHandler(IPeriodoRepository periodos) => _periodos = periodos;

    public async Task<PeriodoDto> Handle(UpdatePeriodoCommand request, CancellationToken ct)
    {
        var periodo = await _periodos.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El período no existe.");

        periodo.Actualizar(request.Descripcion, request.NroLiq);
        if (request.Activo) periodo.Activar();
        else periodo.Desactivar();

        await _periodos.UpdateAsync(periodo, ct);
        return new PeriodoDto(periodo.Id, periodo.Codigo, periodo.Descripcion, periodo.Activo, periodo.NroLiq);
    }
}

public sealed record GetTiposLicenciaAdminQuery : IRequest<IReadOnlyList<TipoLicenciaDto>>;

public sealed class GetTiposLicenciaAdminQueryHandler : IRequestHandler<GetTiposLicenciaAdminQuery,
    IReadOnlyList<TipoLicenciaDto>>
{
    private readonly ITipoLicenciaRepository _tipos;

    public GetTiposLicenciaAdminQueryHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<IReadOnlyList<TipoLicenciaDto>> Handle(GetTiposLicenciaAdminQuery request, CancellationToken ct)
    {
        var tipos = await _tipos.GetAllAsync(ct);
        return tipos
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoLicenciaDto(t.Id, t.Nombre, t.LimiteMensual, t.LimiteAnual, t.Descripcion,
                t.RequiereAdjunto, t.Activo,
                t.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList()))
            .ToList();
    }
}

public sealed record CreateTipoLicenciaCommand(string Nombre, int? LimiteMensual, int? LimiteAnual, string? Descripcion,
    bool RequiereAdjunto, IReadOnlyList<AprobadorRequerido> Niveles) : IRequest<TipoLicenciaDto>;

public sealed class CreateTipoLicenciaCommandValidator : AbstractValidator<CreateTipoLicenciaCommand>
{
    public CreateTipoLicenciaCommandValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.");
        RuleFor(c => c.LimiteMensual).GreaterThan(0).When(c => c.LimiteMensual.HasValue)
            .WithMessage("El límite mensual debe ser mayor a cero.");
        RuleFor(c => c.LimiteAnual).GreaterThan(0).When(c => c.LimiteAnual.HasValue)
            .WithMessage("El límite anual debe ser mayor a cero.");
    }
}

public sealed class CreateTipoLicenciaCommandHandler : IRequestHandler<CreateTipoLicenciaCommand, TipoLicenciaDto>
{
    private readonly ITipoLicenciaRepository _tipos;

    public CreateTipoLicenciaCommandHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<TipoLicenciaDto> Handle(CreateTipoLicenciaCommand request, CancellationToken ct)
    {
        var tipo = new TipoLicencia(request.Nombre, request.LimiteMensual, request.LimiteAnual, request.Descripcion,
            request.RequiereAdjunto);
        foreach (var nivel in request.Niveles)
            tipo.AgregarNivel(nivel);

        await _tipos.AddAsync(tipo, ct);

        return new TipoLicenciaDto(tipo.Id, tipo.Nombre, tipo.LimiteMensual, tipo.LimiteAnual, tipo.Descripcion,
            tipo.RequiereAdjunto, tipo.Activo,
            tipo.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList());
    }
}

public sealed record UpdateTipoLicenciaCommand(Guid Id, string Nombre, int? LimiteMensual, int? LimiteAnual,
    string? Descripcion, bool RequiereAdjunto) : IRequest<TipoLicenciaDto>;

public sealed class UpdateTipoLicenciaCommandHandler : IRequestHandler<UpdateTipoLicenciaCommand, TipoLicenciaDto>
{
    private readonly ITipoLicenciaRepository _tipos;

    public UpdateTipoLicenciaCommandHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<TipoLicenciaDto> Handle(UpdateTipoLicenciaCommand request, CancellationToken ct)
    {
        var tipo = await _tipos.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El tipo de licencia no existe.");

        tipo.Actualizar(request.Nombre, request.LimiteMensual, request.LimiteAnual, request.Descripcion,
            request.RequiereAdjunto);
        await _tipos.UpdateAsync(tipo, ct);

        return new TipoLicenciaDto(tipo.Id, tipo.Nombre, tipo.LimiteMensual, tipo.LimiteAnual, tipo.Descripcion,
            tipo.RequiereAdjunto, tipo.Activo,
            tipo.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList());
    }
}

public sealed record SetActivoTipoLicenciaCommand(Guid Id, bool Activo) : IRequest<Unit>;

public sealed class SetActivoTipoLicenciaCommandHandler : IRequestHandler<SetActivoTipoLicenciaCommand, Unit>
{
    private readonly ITipoLicenciaRepository _tipos;

    public SetActivoTipoLicenciaCommandHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<Unit> Handle(SetActivoTipoLicenciaCommand request, CancellationToken ct)
    {
        var tipo = await _tipos.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El tipo de licencia no existe.");

        if (request.Activo) tipo.Activar();
        else tipo.Desactivar();

        await _tipos.UpdateAsync(tipo, ct);
        return Unit.Value;
    }
}

public sealed record AgregarNivelAprobacionCommand(Guid TipoLicenciaId, AprobadorRequerido RolRequerido)
    : IRequest<TipoLicenciaDto>;

public sealed class AgregarNivelAprobacionCommandHandler : IRequestHandler<AgregarNivelAprobacionCommand, TipoLicenciaDto>
{
    private readonly ITipoLicenciaRepository _tipos;

    public AgregarNivelAprobacionCommandHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<TipoLicenciaDto> Handle(AgregarNivelAprobacionCommand request, CancellationToken ct)
    {
        var tipo = await _tipos.GetByIdAsync(request.TipoLicenciaId, ct)
            ?? throw new EntidadNoEncontradaException("El tipo de licencia no existe.");

        tipo.AgregarNivel(request.RolRequerido);
        await _tipos.UpdateAsync(tipo, ct);

        return new TipoLicenciaDto(tipo.Id, tipo.Nombre, tipo.LimiteMensual, tipo.LimiteAnual, tipo.Descripcion,
            tipo.RequiereAdjunto, tipo.Activo,
            tipo.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList());
    }
}

public sealed record QuitarNivelAprobacionCommand(Guid NivelId) : IRequest<TipoLicenciaDto>;

public sealed class QuitarNivelAprobacionCommandHandler : IRequestHandler<QuitarNivelAprobacionCommand, TipoLicenciaDto>
{
    private readonly ITipoLicenciaRepository _tipos;

    public QuitarNivelAprobacionCommandHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<TipoLicenciaDto> Handle(QuitarNivelAprobacionCommand request, CancellationToken ct)
    {
        var tipo = (await _tipos.GetAllAsync(ct)).FirstOrDefault(t =>
            t.Niveles.Any(n => n.Id == request.NivelId))
            ?? throw new EntidadNoEncontradaException("El nivel de aprobación no existe.");

        tipo.QuitarNivel(request.NivelId);
        await _tipos.UpdateAsync(tipo, ct);

        return new TipoLicenciaDto(tipo.Id, tipo.Nombre, tipo.LimiteMensual, tipo.LimiteAnual, tipo.Descripcion,
            tipo.RequiereAdjunto, tipo.Activo,
            tipo.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList());
    }
}

public sealed record ReordenarNivelesCommand(Guid TipoLicenciaId, IReadOnlyList<Guid> NivelIds) : IRequest<TipoLicenciaDto>;

public sealed class ReordenarNivelesCommandHandler : IRequestHandler<ReordenarNivelesCommand, TipoLicenciaDto>
{
    private readonly ITipoLicenciaRepository _tipos;

    public ReordenarNivelesCommandHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<TipoLicenciaDto> Handle(ReordenarNivelesCommand request, CancellationToken ct)
    {
        var tipo = await _tipos.GetByIdAsync(request.TipoLicenciaId, ct)
            ?? throw new EntidadNoEncontradaException("El tipo de licencia no existe.");

        tipo.Reordenar(request.NivelIds);
        await _tipos.UpdateAsync(tipo, ct);

        return new TipoLicenciaDto(tipo.Id, tipo.Nombre, tipo.LimiteMensual, tipo.LimiteAnual, tipo.Descripcion,
            tipo.RequiereAdjunto, tipo.Activo,
            tipo.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList());
    }
}