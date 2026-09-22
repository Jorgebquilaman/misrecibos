using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.CertificadosCv;

public sealed record CvItemAdjuntoDto(Guid Id, Guid AdjuntoId, string NombreArchivo, string ContentType, long TamanoBytes, DateTime FechaCarga);

public sealed record CvItemDto(Guid Id, string Seccion, string Categoria, string Titulo, string? Institucion,
    string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta, IReadOnlyList<CvItemAdjuntoDto>? Adjuntos = null);

internal static class ValidacionesCvItem
{
    internal static void ValidarFechas(DateOnly desde, DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        if (desde > hoy || hasta > hoy)
            throw new ReglaDeNegocioException("Las fechas no pueden ser futuras.");
    }

    internal static SeccionCvItem ParseSeccion(string seccion) => seccion.Trim().ToLowerInvariant() switch
    {
        "antecedentes-prof-artisticos" => SeccionCvItem.AntecedentesProfArtisticos,
        "produccion" => SeccionCvItem.Produccion,
        "otros-antecedentes" => SeccionCvItem.OtrosAntecedentes,
        _ => throw new ReglaDeNegocioException("La sección no es válida.")
    };

    internal static string ToSeccionString(SeccionCvItem s) => s switch
    {
        SeccionCvItem.AntecedentesProfArtisticos => "antecedentes-prof-artisticos",
        SeccionCvItem.Produccion => "produccion",
        _ => "otros-antecedentes"
    };
}

/// <summary>Lista todos los ítems del CV (3 secciones) del empleado, con sus adjuntos.</summary>
public sealed record ListarCvItemsQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<CvItemDto>>;

public sealed class ListarCvItemsQueryHandler : IRequestHandler<ListarCvItemsQuery, IReadOnlyList<CvItemDto>>
{
    private readonly ICvAntecedenteItemRepository _items;
    private readonly ICvItemAdjuntoRepository _adjuntosItem;
    private readonly IAdjuntoRepository _adjuntos;

    public ListarCvItemsQueryHandler(ICvAntecedenteItemRepository items, ICvItemAdjuntoRepository adjuntosItem,
        IAdjuntoRepository adjuntos)
    {
        _items = items;
        _adjuntosItem = adjuntosItem;
        _adjuntos = adjuntos;
    }

    public async Task<IReadOnlyList<CvItemDto>> Handle(ListarCvItemsQuery request, CancellationToken ct)
    {
        var lista = await _items.GetByEmpleadoAsync(request.EmpleadoId, ct);
        var resultado = new List<CvItemDto>(lista.Count);
        foreach (var it in lista)
        {
            var joins = await _adjuntosItem.GetByItemAsync(it.Id, ct);
            var adjuntosDto = new List<CvItemAdjuntoDto>(joins.Count);
            foreach (var j in joins)
            {
                var adj = await _adjuntos.GetByIdAsync(j.AdjuntoId, ct);
                if (adj is null) continue;
                adjuntosDto.Add(new CvItemAdjuntoDto(j.Id, adj.Id, adj.NombreArchivo, adj.ContentType, adj.TamañoBytes, j.FechaCarga));
            }
            resultado.Add(new CvItemDto(it.Id, ValidacionesCvItem.ToSeccionString(it.Seccion), it.Categoria, it.Titulo,
                it.Institucion, it.Descripcion, it.FechaDesde, it.FechaHasta, adjuntosDto));
        }
        return resultado;
    }
}

/// <summary>Crea un ítem de antecedente/producción en el CV del empleado.</summary>
public sealed record CrearCvItemCommand(Guid EmpleadoId, string Seccion, string Categoria, string Titulo,
    string? Institucion, string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta) : IRequest<CvItemDto>;

public sealed class CrearCvItemCommandHandler : IRequestHandler<CrearCvItemCommand, CvItemDto>
{
    private readonly ICvAntecedenteItemRepository _items;

    public CrearCvItemCommandHandler(ICvAntecedenteItemRepository items) => _items = items;

    public async Task<CvItemDto> Handle(CrearCvItemCommand request, CancellationToken ct)
    {
        var seccion = ValidacionesCvItem.ParseSeccion(request.Seccion);
        ValidacionesCvItem.ValidarFechas(request.FechaDesde, request.FechaHasta);
        if (request.Descripcion is { Length: > 3000 })
            throw new ReglaDeNegocioException("La descripción no puede superar los 3000 caracteres.");

        var item = new CvAntecedenteItem(request.EmpleadoId, seccion, request.Categoria, request.Titulo,
            request.Institucion, request.Descripcion, request.FechaDesde, request.FechaHasta);
        await _items.AddAsync(item, ct);

        return new CvItemDto(item.Id, ValidacionesCvItem.ToSeccionString(item.Seccion), item.Categoria, item.Titulo,
            item.Institucion, item.Descripcion, item.FechaDesde, item.FechaHasta, Array.Empty<CvItemAdjuntoDto>());
    }
}

/// <summary>Edita un ítem propio del empleado.</summary>
public sealed record EditarCvItemCommand(Guid EmpleadoId, Guid ItemId, string Seccion, string Categoria, string Titulo,
    string? Institucion, string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta) : IRequest<Unit>;

public sealed class EditarCvItemCommandHandler : IRequestHandler<EditarCvItemCommand, Unit>
{
    private readonly ICvAntecedenteItemRepository _items;

    public EditarCvItemCommandHandler(ICvAntecedenteItemRepository items) => _items = items;

    public async Task<Unit> Handle(EditarCvItemCommand request, CancellationToken ct)
    {
        var seccion = ValidacionesCvItem.ParseSeccion(request.Seccion);
        ValidacionesCvItem.ValidarFechas(request.FechaDesde, request.FechaHasta);
        if (request.Descripcion is { Length: > 3000 })
            throw new ReglaDeNegocioException("La descripción no puede superar los 3000 caracteres.");

        var item = await _items.GetByIdAsync(request.ItemId, ct)
            ?? throw new EntidadNoEncontradaException("El ítem no existe.");
        if (item.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés modificar un ítem de otro empleado.");
        item.Editar(seccion, request.Categoria, request.Titulo, request.Institucion, request.Descripcion,
            request.FechaDesde, request.FechaHasta);
        await _items.UpdateAsync(item, ct);
        return Unit.Value;
    }
}

/// <summary>Duplica un ítem propio (solo metadatos, sin adjuntos) como nuevo ítem.</summary>
public sealed record DuplicarCvItemCommand(Guid EmpleadoId, Guid ItemId) : IRequest<CvItemDto>;

public sealed class DuplicarCvItemCommandHandler : IRequestHandler<DuplicarCvItemCommand, CvItemDto>
{
    private readonly ICvAntecedenteItemRepository _items;

    public DuplicarCvItemCommandHandler(ICvAntecedenteItemRepository items) => _items = items;

    public async Task<CvItemDto> Handle(DuplicarCvItemCommand request, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(request.ItemId, ct)
            ?? throw new EntidadNoEncontradaException("El ítem no existe.");
        if (item.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés duplicar un ítem de otro empleado.");
        var clon = new CvAntecedenteItem(item.EmpleadoId, item.Seccion, item.Categoria, item.Titulo,
            item.Institucion, item.Descripcion, item.FechaDesde, item.FechaHasta);
        await _items.AddAsync(clon, ct);
        return new CvItemDto(clon.Id, ValidacionesCvItem.ToSeccionString(clon.Seccion), clon.Categoria, clon.Titulo,
            clon.Institucion, clon.Descripcion, clon.FechaDesde, clon.FechaHasta, Array.Empty<CvItemAdjuntoDto>());
    }
}

/// <summary>Elimina un ítem propio junto con sus adjuntos (storage incluido).</summary>
public sealed record EliminarCvItemCommand(Guid EmpleadoId, Guid ItemId) : IRequest<Unit>;

public sealed class EliminarCvItemCommandHandler : IRequestHandler<EliminarCvItemCommand, Unit>
{
    private readonly ICvAntecedenteItemRepository _items;
    private readonly ICvItemAdjuntoRepository _adjuntosItem;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarCvItemCommandHandler(ICvAntecedenteItemRepository items, ICvItemAdjuntoRepository adjuntosItem,
        IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _items = items;
        _adjuntosItem = adjuntosItem;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarCvItemCommand request, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(request.ItemId, ct)
            ?? throw new EntidadNoEncontradaException("El ítem no existe.");
        if (item.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar un ítem de otro empleado.");
        var joins = await _adjuntosItem.GetByItemAsync(item.Id, ct);
        foreach (var j in joins)
        {
            var adj = await _adjuntos.GetByIdAsync(j.AdjuntoId, ct);
            if (adj is not null) try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
            await _adjuntosItem.DeleteAsync(j, ct);
        }
        await _items.DeleteAsync(item, ct);
        return Unit.Value;
    }
}

public sealed record SubirCvItemAdjuntoCommand(Guid EmpleadoId, Guid ItemId, string ArchivoNombre, string ContentType, Stream Contenido) : IRequest<CvItemAdjuntoDto>;

public sealed class SubirCvItemAdjuntoCommandHandler : IRequestHandler<SubirCvItemAdjuntoCommand, CvItemAdjuntoDto>
{
    private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxBytes = 10 * 1024 * 1024;
    private readonly ICvAntecedenteItemRepository _items;
    private readonly ICvItemAdjuntoRepository _adjuntosItem;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public SubirCvItemAdjuntoCommandHandler(ICvAntecedenteItemRepository items, ICvItemAdjuntoRepository adjuntosItem,
        IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _items = items;
        _adjuntosItem = adjuntosItem;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<CvItemAdjuntoDto> Handle(SubirCvItemAdjuntoCommand request, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(request.ItemId, ct)
            ?? throw new EntidadNoEncontradaException("El ítem no existe.");
        if (item.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés agregar anexos a un ítem de otro empleado.");
        var existentes = await _adjuntosItem.GetByItemAsync(request.ItemId, ct);
        if (existentes.Count >= 5)
            throw new ReglaDeNegocioException("Máximo 5 anexos por ítem.");
        var extension = Path.GetExtension(request.ArchivoNombre).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            throw new ReglaDeNegocioException("Solo se permiten archivos PDF, JPG o PNG.");
        if (request.Contenido.Length > MaxBytes)
            throw new ReglaDeNegocioException("El archivo supera los 10 MB.");
        var adjuntoId = Guid.NewGuid();
        var storageKey = $"cv-item/{adjuntoId}{extension}";
        await _storage.GuardarAsync(storageKey, request.ContentType, request.Contenido, ct);
        var adjunto = new Adjunto(request.ArchivoNombre, request.ContentType, request.Contenido.Length, storageKey, request.EmpleadoId);
        adjunto.VincularAEntidad("cv_item", adjuntoId);
        await _adjuntos.AddAsync(adjunto, ct);
        var join = new CvAntecedenteItemAdjunto(request.ItemId, adjunto.Id);
        await _adjuntosItem.AddAsync(join, ct);
        return new CvItemAdjuntoDto(join.Id, adjunto.Id, adjunto.NombreArchivo, adjunto.ContentType, adjunto.TamañoBytes, join.FechaCarga);
    }
}

public sealed record EliminarCvItemAdjuntoCommand(Guid EmpleadoId, Guid ItemId, Guid AdjuntoId) : IRequest<Unit>;

public sealed class EliminarCvItemAdjuntoCommandHandler : IRequestHandler<EliminarCvItemAdjuntoCommand, Unit>
{
    private readonly ICvAntecedenteItemRepository _items;
    private readonly ICvItemAdjuntoRepository _adjuntosItem;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarCvItemAdjuntoCommandHandler(ICvAntecedenteItemRepository items, ICvItemAdjuntoRepository adjuntosItem,
        IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _items = items;
        _adjuntosItem = adjuntosItem;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarCvItemAdjuntoCommand request, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(request.ItemId, ct)
            ?? throw new EntidadNoEncontradaException("El ítem no existe.");
        if (item.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar anexos de un ítem de otro empleado.");
        var join = await _adjuntosItem.GetByIdAsync(request.AdjuntoId, ct)
            ?? await _adjuntosItem.GetByAdjuntoIdAsync(request.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El anexo no existe.");
        if (join.ItemId != request.ItemId)
            throw new EntidadNoEncontradaException("El anexo no pertenece a ese ítem.");
        var adj = await _adjuntos.GetByIdAsync(join.AdjuntoId, ct);
        if (adj is not null) try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
        await _adjuntosItem.DeleteAsync(join, ct);
        return Unit.Value;
    }
}

public sealed record DescargarCvItemAdjuntoQuery(Guid ItemId, Guid AdjuntoId, Guid EmpleadoIdSolicitante) : IRequest<(Stream Contenido, string NombreArchivo, string ContentType)>;

public sealed class DescargarCvItemAdjuntoQueryHandler : IRequestHandler<DescargarCvItemAdjuntoQuery, (Stream, string, string)>
{
    private readonly ICvAntecedenteItemRepository _items;
    private readonly ICvItemAdjuntoRepository _adjuntosItem;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public DescargarCvItemAdjuntoQueryHandler(ICvAntecedenteItemRepository items, ICvItemAdjuntoRepository adjuntosItem,
        IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _items = items;
        _adjuntosItem = adjuntosItem;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<(Stream, string, string)> Handle(DescargarCvItemAdjuntoQuery request, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(request.ItemId, ct)
            ?? throw new EntidadNoEncontradaException("El ítem no existe.");
        if (item.EmpleadoId != request.EmpleadoIdSolicitante)
            throw new ReglaDeNegocioException("No podés ver anexos de un ítem de otro empleado.");
        var join = await _adjuntosItem.GetByIdAsync(request.AdjuntoId, ct)
            ?? await _adjuntosItem.GetByAdjuntoIdAsync(request.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El anexo no existe.");
        if (join.ItemId != request.ItemId)
            throw new EntidadNoEncontradaException("El anexo no pertenece a ese ítem.");
        var adj = await _adjuntos.GetByIdAsync(join.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El archivo no existe.");
        var stream = await _storage.AbrirAsync(adj.StorageKey, ct);
        return (stream, adj.NombreArchivo, adj.ContentType);
    }
}
