using MediatR;
using Microsoft.Extensions.Logging;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.CertificadosCv;

public sealed record CertificadoCvDto(
    Guid Id, string Nombre, string Institucion, string Tipo, DateOnly FechaObtencion,
    string Estado, string? ComentarioRevision, Guid AdjuntoId, string NombreArchivo,
    long TamanoBytes, DateTime FechaCarga);

public sealed record CertificadoCvAdminDto(
    Guid Id, Guid EmpleadoId, int Legajo, string EmpleadoNombre, string? Area,
    string Nombre, string Institucion, string Tipo, DateOnly FechaObtencion,
    string Estado, string? ComentarioRevision, Guid AdjuntoId, string NombreArchivo,
    long TamanoBytes, DateTime FechaCarga);

/// <summary>Subida de un certificado de curso/carrera (CV) con su archivo adjunto.</summary>
public sealed record SubirCertificadoCvCommand(
    Guid EmpleadoId, string Nombre, string Institucion, string Tipo, DateOnly FechaObtencion,
    string ArchivoNombre, string ContentType, Stream Contenido) : IRequest<CertificadoCvDto>;

public sealed class SubirCertificadoCvCommandHandler : IRequestHandler<SubirCertificadoCvCommand, CertificadoCvDto>
{
    private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxBytes = 10 * 1024 * 1024;

    private readonly ICertificadoCvRepository _certificados;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public SubirCertificadoCvCommandHandler(ICertificadoCvRepository certificados, IAdjuntoRepository adjuntos,
        IFileStoragePort storage)
    {
        _certificados = certificados;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<CertificadoCvDto> Handle(SubirCertificadoCvCommand request, CancellationToken ct)
    {
        if (request.FechaObtencion > DateOnly.FromDateTime(DateTime.Today))
            throw new ReglaDeNegocioException("La fecha de obtención no puede ser futura.");

        var extension = Path.GetExtension(request.ArchivoNombre).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            throw new ReglaDeNegocioException("Solo se permiten archivos PDF, JPG o PNG.");
        if (request.Contenido.Length > MaxBytes)
            throw new ReglaDeNegocioException("El archivo supera los 10 MB.");

        var tipo = ParseTipo(request.Tipo);

        var adjuntoId = Guid.NewGuid();
        var storageKey = $"cv/{adjuntoId}{extension}";
        await _storage.GuardarAsync(storageKey, request.ContentType, request.Contenido, ct);

        var adjunto = new Adjunto(request.ArchivoNombre, request.ContentType, request.Contenido.Length,
            storageKey, request.EmpleadoId);
        adjunto.VincularAEntidad("certificado_cv", adjuntoId);
        await _adjuntos.AddAsync(adjunto, ct);

        var certificado = new CertificadoCurso(request.EmpleadoId, request.Nombre, request.Institucion,
            tipo, request.FechaObtencion, adjunto.Id);
        await _certificados.AddAsync(certificado, ct);

        return Mapear(certificado, adjunto);
    }

    internal static TipoEstudio ParseTipo(string tipo) => tipo.Trim().ToLowerInvariant() switch
    {
        "curso" => TipoEstudio.Curso,
        "taller" => TipoEstudio.Taller,
        "diplomatura" => TipoEstudio.Diplomatura,
        "carrera" => TipoEstudio.Carrera,
        "posgrado" => TipoEstudio.Posgrado,
        "otro" => TipoEstudio.Otro,
        _ => throw new ReglaDeNegocioException("El tipo de estudio no es válido.")
    };

    internal static CertificadoCvDto Mapear(CertificadoCurso c, Adjunto a) =>
        new(c.Id, c.Nombre, c.Institucion, c.Tipo.ToString(), c.FechaObtencion,
            c.Estado.ToString(), c.ComentarioRevision, a.Id, a.NombreArchivo, a.TamañoBytes, c.FechaCarga);
}

/// <summary>Edita los datos de un certificado propio (sin reemplazar el archivo); vuelve a Pendiente para revisión de RRHH.</summary>
public sealed record EditarCertificadoCvCommand(
    Guid EmpleadoId, Guid CertificadoId, string Nombre, string Institucion, string Tipo, DateOnly FechaObtencion) : IRequest<Unit>;

public sealed class EditarCertificadoCvCommandHandler : IRequestHandler<EditarCertificadoCvCommand, Unit>
{
    private readonly ICertificadoCvRepository _certificados;

    public EditarCertificadoCvCommandHandler(ICertificadoCvRepository certificados) => _certificados = certificados;

    public async Task<Unit> Handle(EditarCertificadoCvCommand request, CancellationToken ct)
    {
        if (request.FechaObtencion > DateOnly.FromDateTime(DateTime.Today))
            throw new ReglaDeNegocioException("La fecha de obtención no puede ser futura.");
        var tipo = SubirCertificadoCvCommandHandler.ParseTipo(request.Tipo);
        var cert = await _certificados.GetByIdAsync(request.CertificadoId, ct)
            ?? throw new EntidadNoEncontradaException("El certificado no existe.");
        if (cert.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés modificar un certificado de otro empleado.");
        cert.Editar(request.Nombre, request.Institucion, tipo, request.FechaObtencion);
        await _certificados.UpdateAsync(cert, ct);
        return Unit.Value;
    }
}

/// <summary>Elimina un certificado propio junto con su archivo en storage.</summary>
public sealed record EliminarCertificadoCvCommand(Guid EmpleadoId, Guid CertificadoId) : IRequest<Unit>;

public sealed class EliminarCertificadoCvCommandHandler : IRequestHandler<EliminarCertificadoCvCommand, Unit>
{
    private readonly ICertificadoCvRepository _certificados;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarCertificadoCvCommandHandler(ICertificadoCvRepository certificados, IAdjuntoRepository adjuntos,
        IFileStoragePort storage)
    {
        _certificados = certificados;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarCertificadoCvCommand request, CancellationToken ct)
    {
        var cert = await _certificados.GetByIdAsync(request.CertificadoId, ct)
            ?? throw new EntidadNoEncontradaException("El certificado no existe.");
        if (cert.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar un certificado de otro empleado.");
        var adj = await _adjuntos.GetByIdAsync(cert.AdjuntoId, ct);
        if (adj is not null) try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
        await _certificados.DeleteAsync(cert, ct);
        return Unit.Value;
    }
}

/// <summary>"Mis certificados CV": lista de certificados subidos por el empleado.</summary>
public sealed record GetMisCertificadosCvQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<CertificadoCvDto>>;

public sealed class GetMisCertificadosCvQueryHandler : IRequestHandler<GetMisCertificadosCvQuery, IReadOnlyList<CertificadoCvDto>>
{
    private readonly ICertificadoCvRepository _certificados;
    private readonly IAdjuntoRepository _adjuntos;

    public GetMisCertificadosCvQueryHandler(ICertificadoCvRepository certificados, IAdjuntoRepository adjuntos)
    {
        _certificados = certificados;
        _adjuntos = adjuntos;
    }

    public async Task<IReadOnlyList<CertificadoCvDto>> Handle(GetMisCertificadosCvQuery request, CancellationToken ct)
    {
        var lista = (await _certificados.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(c => c.FechaCarga)
            .ToList();

        var resultado = new List<CertificadoCvDto>(lista.Count);
        foreach (var c in lista)
        {
            var adjunto = await _adjuntos.GetByIdAsync(c.AdjuntoId, ct);
            resultado.Add(new CertificadoCvDto(c.Id, c.Nombre, c.Institucion, c.Tipo.ToString(), c.FechaObtencion,
                c.Estado.ToString(), c.ComentarioRevision, c.AdjuntoId, adjunto?.NombreArchivo ?? "",
                adjunto?.TamañoBytes ?? 0, c.FechaCarga));
        }
        return resultado;
    }
}

/// <summary>Listado completo para RRHH/Administración.</summary>
public sealed record ListarCertificadosCvQuery(string? Estado)
    : IRequest<IReadOnlyList<CertificadoCvAdminDto>>;

public sealed class ListarCertificadosCvQueryHandler : IRequestHandler<ListarCertificadosCvQuery, IReadOnlyList<CertificadoCvAdminDto>>
{
    private readonly ICertificadoCvRepository _certificados;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;
    private readonly IAdjuntoRepository _adjuntos;

    public ListarCertificadosCvQueryHandler(ICertificadoCvRepository certificados, IEmpleadoRepository empleados,
        IAreaRepository areas, IAdjuntoRepository adjuntos)
    {
        _certificados = certificados;
        _empleados = empleados;
        _areas = areas;
        _adjuntos = adjuntos;
    }

    public async Task<IReadOnlyList<CertificadoCvAdminDto>> Handle(ListarCertificadosCvQuery request, CancellationToken ct)
    {
        var todos = (await _certificados.GetAllAsync(ct)).ToList();

        if (!string.IsNullOrWhiteSpace(request.Estado) &&
            Enum.TryParse<EstadoCertificadoCv>(request.Estado, ignoreCase: true, out var estadoFiltro))
        {
            todos = todos.Where(c => c.Estado == estadoFiltro).ToList();
        }

        var empleados = (await _empleados.GetAllAsync(ct)).ToDictionary(e => e.Id);
        var areas = (await _areas.GetAllAsync(ct)).ToDictionary(a => a.Id, a => a.Nombre);

        var resultado = new List<CertificadoCvAdminDto>(todos.Count);
        foreach (var c in todos.OrderByDescending(c => c.FechaCarga))
        {
            empleados.TryGetValue(c.EmpleadoId, out var empleado);
            var adjunto = await _adjuntos.GetByIdAsync(c.AdjuntoId, ct);
            resultado.Add(new CertificadoCvAdminDto(
                c.Id, c.EmpleadoId, empleado?.Legajo ?? 0,
                empleado is null ? "(empleado eliminado)" : $"{empleado.Apellido}, {empleado.Nombre}",
                empleado?.AreaId is { } areaId && areas.TryGetValue(areaId, out var area) ? area : null,
                c.Nombre, c.Institucion, c.Tipo.ToString(), c.FechaObtencion,
                c.Estado.ToString(), c.ComentarioRevision, c.AdjuntoId,
                adjunto?.NombreArchivo ?? "", adjunto?.TamañoBytes ?? 0, c.FechaCarga));
        }
        return resultado;
    }
}

/// <summary>Genera los datos del CV del empleado (datos personales + experiencias + certificados + archivos adjuntos).</summary>
public sealed record GenerarMiCvQuery(Guid EmpleadoId) : IRequest<CvCompletoDto>;

public sealed record CvCompletoDto(DatosCvPdf Datos, IReadOnlyList<ArchivoCertificadoCv> Archivos);

public sealed class GenerarMiCvQueryHandler : IRequestHandler<GenerarMiCvQuery, CvCompletoDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly ICertificadoCvRepository _certificados;
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICvExperienciaAdjuntoRepository _adjuntosExp;
    private readonly ICvAntecedenteAdjuntoRepository _adjuntosAnt;
    private readonly ICvAntecedenteItemRepository _itemsCv;
    private readonly ICvItemAdjuntoRepository _adjuntosItemCv;
    private readonly IAreaRepository _areas;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public GenerarMiCvQueryHandler(IEmpleadoRepository empleados, ICertificadoCvRepository certificados,
        ICvExperienciaRepository experiencias, ICvAntecedenteAcademicoRepository antecedentes,
        ICvExperienciaAdjuntoRepository adjuntosExp, ICvAntecedenteAdjuntoRepository adjuntosAnt,
        ICvAntecedenteItemRepository itemsCv, ICvItemAdjuntoRepository adjuntosItemCv,
        IAreaRepository areas, IAdjuntoRepository adjuntos,
        IFileStoragePort storage)
    {
        _empleados = empleados;
        _certificados = certificados;
        _experiencias = experiencias;
        _antecedentes = antecedentes;
        _adjuntosExp = adjuntosExp;
        _adjuntosAnt = adjuntosAnt;
        _itemsCv = itemsCv;
        _adjuntosItemCv = adjuntosItemCv;
        _areas = areas;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<CvCompletoDto> Handle(GenerarMiCvQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var lista = (await _certificados.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderBy(c => c.FechaObtencion)
            .ToList();

        var certificados = lista
            .Select(c => new DatoCvCertificado(c.Nombre, c.Institucion, c.Tipo.ToString(), c.FechaObtencion))
            .ToList();

        var experiencias = (await _experiencias.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(e => e.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today))
            .ThenByDescending(e => e.FechaDesde)
            .Select(e => new DatoCvExperiencia(e.Puesto, e.Institucion, e.Descripcion, e.FechaDesde, e.FechaHasta))
            .ToList();

        var antecedentes = (await _antecedentes.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(a => a.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today))
            .ThenByDescending(a => a.FechaDesde)
            .Select(a => new DatoCvAntecedente(a.Titulo, a.Institucion, a.Nivel.ToString(), a.Descripcion, a.FechaDesde, a.FechaHasta))
            .ToList();

        var itemsCv = (await _itemsCv.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .Select(i => new DatoCvItem(ToSeccionNombre(i.Seccion), i.Categoria, i.Titulo, i.Institucion,
                i.Descripcion, i.FechaDesde, i.FechaHasta))
            .ToList();

        string? area = null;
        if (empleado.AreaId is { } areaId)
        {
            var a = await _areas.GetAllAsync(ct);
            area = a.FirstOrDefault(x => x.Id == areaId)?.Nombre;
        }

        var archivos = new List<ArchivoCertificadoCv>();
        foreach (var c in lista.OrderByDescending(c => c.FechaObtencion))
        {
            var adjunto = await _adjuntos.GetByIdAsync(c.AdjuntoId, ct);
            if (adjunto is null) continue;

            await using var stream = await _storage.AbrirAsync(adjunto.StorageKey, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            // En el índice, los certificados se muestran con el nombre del curso/carrera
            archivos.Add(new ArchivoCertificadoCv(c.Nombre, adjunto.ContentType, ms.ToArray(),
                c.Institucion, c.Tipo.ToString(), c.FechaObtencion.ToString("dd/MM/yyyy")));
        }
        foreach (var ant in (await _antecedentes.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(a => a.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today))
            .ThenByDescending(a => a.FechaDesde))
        {
            var adjuntosAnt = await _adjuntosAnt.GetByAntecedenteAsync(ant.Id, ct);
            foreach (var aa in adjuntosAnt)
            {
                var adjunto = await _adjuntos.GetByIdAsync(aa.AdjuntoId, ct);
                if (adjunto is null) continue;
                await using var stream = await _storage.AbrirAsync(adjunto.StorageKey, ct);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                var periodoAnt = $"{ant.FechaDesde:MM/yyyy} – {(ant.FechaHasta?.ToString("MM/yyyy") ?? "actualidad")}";
                archivos.Add(new ArchivoCertificadoCv($"{ant.Titulo} — {adjunto.NombreArchivo}", adjunto.ContentType, ms.ToArray(),
                    ant.Institucion, "Antecedente académico", periodoAnt));
            }
        }
        foreach (var exp in await _experiencias.GetByEmpleadoAsync(request.EmpleadoId, ct))
        {
            var adjuntosExp = await _adjuntosExp.GetByExperienciaAsync(exp.Id, ct);
            foreach (var ae in adjuntosExp)
            {
                var adjunto = await _adjuntos.GetByIdAsync(ae.AdjuntoId, ct);
                if (adjunto is null) continue;
                await using var stream = await _storage.AbrirAsync(adjunto.StorageKey, ct);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                var periodoExp = $"{exp.FechaDesde:MM/yyyy} – {(exp.FechaHasta?.ToString("MM/yyyy") ?? "actualidad")}";
                archivos.Add(new ArchivoCertificadoCv($"{exp.Puesto} — {adjunto.NombreArchivo}", adjunto.ContentType, ms.ToArray(),
                    exp.Institucion, "Experiencia laboral", periodoExp));
            }
        }

        foreach (var item in await _itemsCv.GetByEmpleadoAsync(request.EmpleadoId, ct))
        {
            var joinsItem = await _adjuntosItemCv.GetByItemAsync(item.Id, ct);
            foreach (var j in joinsItem)
            {
                var adjunto = await _adjuntos.GetByIdAsync(j.AdjuntoId, ct);
                if (adjunto is null) continue;
                await using var stream = await _storage.AbrirAsync(adjunto.StorageKey, ct);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                var periodoItem = $"{item.FechaDesde:MM/yyyy} – {(item.FechaHasta?.ToString("MM/yyyy") ?? "actualidad")}";
                archivos.Add(new ArchivoCertificadoCv($"{item.Titulo} — {adjunto.NombreArchivo}", adjunto.ContentType, ms.ToArray(),
                    item.Institucion, item.Categoria, periodoItem));
            }
        }

        return new CvCompletoDto(
            new DatosCvPdf(
                new DatosCvEmpleado(empleado.NombreCompleto, empleado.Legajo, empleado.Correo.Valor,
                    empleado.Dni, area, empleado.CvObservaciones, empleado.CvTelefono),
                certificados,
                experiencias,
                antecedentes,
                itemsCv),
            archivos);
    }

    private static string ToSeccionNombre(SeccionCvItem s) => s switch
    {
        SeccionCvItem.AntecedentesProfArtisticos => "Antecedentes profesionales y/o artísticos",
        SeccionCvItem.Produccion => "Producción",
        _ => "Otros antecedentes"
    };
}

public sealed record ExperienciaAdjuntoDto(Guid Id, Guid AdjuntoId, string NombreArchivo, string ContentType, long TamanoBytes, DateTime FechaCarga);

public sealed record ExperienciaCvDto(Guid Id, string Puesto, string Institucion, string? Descripcion,
    DateOnly FechaDesde, DateOnly? FechaHasta, IReadOnlyList<ExperienciaAdjuntoDto>? Adjuntos = null);

public sealed record AntecedenteAcademicoDto(Guid Id, string Titulo, string Institucion, string Nivel,
    string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta,
    Guid AdjuntoId, string NombreArchivo, long TamanoBytes, DateTime FechaCarga,
    IReadOnlyList<ExperienciaAdjuntoDto>? Adjuntos = null);

/// <summary>Lista las experiencias laborales declaradas por el propio empleado.</summary>
public sealed record ListarMisExperienciasQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<ExperienciaCvDto>>;

public sealed class ListarMisExperienciasQueryHandler : IRequestHandler<ListarMisExperienciasQuery, IReadOnlyList<ExperienciaCvDto>>
{
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvExperienciaAdjuntoRepository _adjuntosExp;
    private readonly IAdjuntoRepository _adjuntos;

    public ListarMisExperienciasQueryHandler(ICvExperienciaRepository experiencias, ICvExperienciaAdjuntoRepository adjuntosExp, IAdjuntoRepository adjuntos)
    {
        _experiencias = experiencias;
        _adjuntosExp = adjuntosExp;
        _adjuntos = adjuntos;
    }

    public async Task<IReadOnlyList<ExperienciaCvDto>> Handle(ListarMisExperienciasQuery request, CancellationToken ct)
    {
        var exps = (await _experiencias.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(e => e.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today))
            .ThenByDescending(e => e.FechaDesde)
            .ToList();
        var resultado = new List<ExperienciaCvDto>(exps.Count);
        foreach (var e in exps)
        {
            var adjuntosExp = await _adjuntosExp.GetByExperienciaAsync(e.Id, ct);
            var adjuntosDto = new List<ExperienciaAdjuntoDto>(adjuntosExp.Count);
            foreach (var ae in adjuntosExp)
            {
                var adj = await _adjuntos.GetByIdAsync(ae.AdjuntoId, ct);
                if (adj is null) continue;
                adjuntosDto.Add(new ExperienciaAdjuntoDto(ae.Id, adj.Id, adj.NombreArchivo, adj.ContentType, adj.TamañoBytes, ae.FechaCarga));
            }
            resultado.Add(new ExperienciaCvDto(e.Id, e.Puesto, e.Institucion, e.Descripcion, e.FechaDesde, e.FechaHasta, adjuntosDto));
        }
        return resultado;
    }
}

/// <summary>Crea una experiencia laboral en el CV del propio empleado.</summary>
public sealed record CrearExperienciaCvCommand(
    Guid EmpleadoId, string Puesto, string Institucion, string? Descripcion,
    DateOnly FechaDesde, DateOnly? FechaHasta) : IRequest<ExperienciaCvDto>;

public sealed class CrearExperienciaCvCommandHandler : IRequestHandler<CrearExperienciaCvCommand, ExperienciaCvDto>
{
    private readonly ICvExperienciaRepository _experiencias;

    public CrearExperienciaCvCommandHandler(ICvExperienciaRepository experiencias) => _experiencias = experiencias;

    public async Task<ExperienciaCvDto> Handle(CrearExperienciaCvCommand request, CancellationToken ct)
    {
        ValidacionesExperienciaCv.ValidarFechas(request.FechaDesde, request.FechaHasta);
        if (request.Descripcion is { Length: > 3000 })
            throw new ReglaDeNegocioException("La descripción no puede superar los 3000 caracteres.");

        var experiencia = new CvExperiencia(request.EmpleadoId, request.Puesto, request.Institucion,
            request.Descripcion, request.FechaDesde, request.FechaHasta);
        await _experiencias.AddAsync(experiencia, ct);

        return new ExperienciaCvDto(experiencia.Id, experiencia.Puesto, experiencia.Institucion,
            experiencia.Descripcion, experiencia.FechaDesde, experiencia.FechaHasta, Array.Empty<ExperienciaAdjuntoDto>());
    }
}

/// <summary>Edita una experiencia laboral propia del empleado.</summary>
public sealed record EditarExperienciaCvCommand(
    Guid EmpleadoId, Guid ExperienciaId, string Puesto, string Institucion, string? Descripcion,
    DateOnly FechaDesde, DateOnly? FechaHasta) : IRequest<Unit>;

public sealed class EditarExperienciaCvCommandHandler : IRequestHandler<EditarExperienciaCvCommand, Unit>
{
    private readonly ICvExperienciaRepository _experiencias;

    public EditarExperienciaCvCommandHandler(ICvExperienciaRepository experiencias) => _experiencias = experiencias;

    public async Task<Unit> Handle(EditarExperienciaCvCommand request, CancellationToken ct)
    {
        ValidacionesExperienciaCv.ValidarFechas(request.FechaDesde, request.FechaHasta);
        if (request.Descripcion is { Length: > 3000 })
            throw new ReglaDeNegocioException("La descripción no puede superar los 3000 caracteres.");

        var experiencia = await _experiencias.GetByIdAsync(request.ExperienciaId, ct)
            ?? throw new EntidadNoEncontradaException("La experiencia no existe.");
        if (experiencia.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés modificar una experiencia de otro empleado.");

        experiencia.Editar(request.Puesto, request.Institucion, request.Descripcion,
            request.FechaDesde, request.FechaHasta);
        await _experiencias.UpdateAsync(experiencia, ct);
        return Unit.Value;
    }
}

/// <summary>Elimina una experiencia laboral propia del empleado.</summary>
public sealed record EliminarExperienciaCvCommand(Guid EmpleadoId, Guid ExperienciaId) : IRequest<Unit>;

public sealed class EliminarExperienciaCvCommandHandler : IRequestHandler<EliminarExperienciaCvCommand, Unit>
{
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvExperienciaAdjuntoRepository _adjuntosExp;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarExperienciaCvCommandHandler(ICvExperienciaRepository experiencias, ICvExperienciaAdjuntoRepository adjuntosExp, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _experiencias = experiencias;
        _adjuntosExp = adjuntosExp;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarExperienciaCvCommand request, CancellationToken ct)
    {
        var experiencia = await _experiencias.GetByIdAsync(request.ExperienciaId, ct)
            ?? throw new EntidadNoEncontradaException("La experiencia no existe.");
        if (experiencia.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar una experiencia de otro empleado.");

        var adjuntosExp = await _adjuntosExp.GetByExperienciaAsync(experiencia.Id, ct);
        foreach (var ae in adjuntosExp)
        {
            var adj = await _adjuntos.GetByIdAsync(ae.AdjuntoId, ct);
            if (adj is not null) try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
            await _adjuntosExp.DeleteAsync(ae, ct);
        }

        await _experiencias.DeleteAsync(experiencia, ct);
        return Unit.Value;
    }
}

public sealed record SubirExperienciaAdjuntoCommand(Guid EmpleadoId, Guid ExperienciaId, string ArchivoNombre, string ContentType, Stream Contenido) : IRequest<ExperienciaAdjuntoDto>;

public sealed class SubirExperienciaAdjuntoCommandHandler : IRequestHandler<SubirExperienciaAdjuntoCommand, ExperienciaAdjuntoDto>
{
    private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxBytes = 10 * 1024 * 1024;
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvExperienciaAdjuntoRepository _adjuntosExp;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public SubirExperienciaAdjuntoCommandHandler(ICvExperienciaRepository experiencias, ICvExperienciaAdjuntoRepository adjuntosExp, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _experiencias = experiencias;
        _adjuntosExp = adjuntosExp;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<ExperienciaAdjuntoDto> Handle(SubirExperienciaAdjuntoCommand request, CancellationToken ct)
    {
        var experiencia = await _experiencias.GetByIdAsync(request.ExperienciaId, ct)
            ?? throw new EntidadNoEncontradaException("La experiencia no existe.");
        if (experiencia.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés agregar anexos a una experiencia de otro empleado.");
        var existentes = await _adjuntosExp.GetByExperienciaAsync(request.ExperienciaId, ct);
        if (existentes.Count >= 5)
            throw new ReglaDeNegocioException("Máximo 5 anexos por experiencia.");
        var extension = Path.GetExtension(request.ArchivoNombre).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            throw new ReglaDeNegocioException("Solo se permiten archivos PDF, JPG o PNG.");
        if (request.Contenido.Length > MaxBytes)
            throw new ReglaDeNegocioException("El archivo supera los 10 MB.");
        var adjuntoId = Guid.NewGuid();
        var storageKey = $"cv-experiencia/{adjuntoId}{extension}";
        await _storage.GuardarAsync(storageKey, request.ContentType, request.Contenido, ct);
        var adjunto = new Adjunto(request.ArchivoNombre, request.ContentType, request.Contenido.Length, storageKey, request.EmpleadoId);
        adjunto.VincularAEntidad("experiencia_cv", adjuntoId);
        await _adjuntos.AddAsync(adjunto, ct);
        var expAdj = new CvExperienciaAdjunto(request.ExperienciaId, adjunto.Id);
        await _adjuntosExp.AddAsync(expAdj, ct);
        return new ExperienciaAdjuntoDto(expAdj.Id, adjunto.Id, adjunto.NombreArchivo, adjunto.ContentType, adjunto.TamañoBytes, expAdj.FechaCarga);
    }
}

public sealed record EliminarExperienciaAdjuntoCommand(Guid EmpleadoId, Guid ExperienciaId, Guid AdjuntoId) : IRequest<Unit>;

public sealed class EliminarExperienciaAdjuntoCommandHandler : IRequestHandler<EliminarExperienciaAdjuntoCommand, Unit>
{
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvExperienciaAdjuntoRepository _adjuntosExp;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarExperienciaAdjuntoCommandHandler(ICvExperienciaRepository experiencias, ICvExperienciaAdjuntoRepository adjuntosExp, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _experiencias = experiencias;
        _adjuntosExp = adjuntosExp;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarExperienciaAdjuntoCommand request, CancellationToken ct)
    {
        var experiencia = await _experiencias.GetByIdAsync(request.ExperienciaId, ct)
            ?? throw new EntidadNoEncontradaException("La experiencia no existe.");
        if (experiencia.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar anexos de una experiencia de otro empleado.");
        var expAdj = await _adjuntosExp.GetByIdAsync(request.AdjuntoId, ct)
            ?? await _adjuntosExp.GetByAdjuntoIdAsync(request.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El anexo no existe.");
        if (expAdj.ExperienciaId != request.ExperienciaId)
            throw new EntidadNoEncontradaException("El anexo no pertenece a esa experiencia.");
        var adj = await _adjuntos.GetByIdAsync(expAdj.AdjuntoId, ct);
        if (adj is not null) try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
        await _adjuntosExp.DeleteAsync(expAdj, ct);
        return Unit.Value;
    }
}

public sealed record DescargarExperienciaAdjuntoQuery(Guid ExperienciaId, Guid AdjuntoId, Guid EmpleadoIdSolicitante, IReadOnlyCollection<string> RolesSolicitante) : IRequest<(Stream Contenido, string NombreArchivo, string ContentType)>;

public sealed class DescargarExperienciaAdjuntoQueryHandler : IRequestHandler<DescargarExperienciaAdjuntoQuery, (Stream, string, string)>
{
    private readonly ICvExperienciaRepository _experiencias;
    private readonly ICvExperienciaAdjuntoRepository _adjuntosExp;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public DescargarExperienciaAdjuntoQueryHandler(ICvExperienciaRepository experiencias, ICvExperienciaAdjuntoRepository adjuntosExp, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _experiencias = experiencias;
        _adjuntosExp = adjuntosExp;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<(Stream, string, string)> Handle(DescargarExperienciaAdjuntoQuery request, CancellationToken ct)
    {
        var exp = await _experiencias.GetByIdAsync(request.ExperienciaId, ct)
            ?? throw new EntidadNoEncontradaException("La experiencia no existe.");
        var expAdj = await _adjuntosExp.GetByIdAsync(request.AdjuntoId, ct)
            ?? await _adjuntosExp.GetByAdjuntoIdAsync(request.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El anexo no existe.");
        if (expAdj.ExperienciaId != request.ExperienciaId)
            throw new EntidadNoEncontradaException("El anexo no pertenece a esa experiencia.");
        var adj = await _adjuntos.GetByIdAsync(expAdj.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El archivo no existe.");
        var stream = await _storage.AbrirAsync(adj.StorageKey, ct);
        return (stream, adj.NombreArchivo, adj.ContentType);
    }
}

/// <summary>Guarda el teléfono de contacto del CV del empleado.</summary>
public sealed record GuardarTelefonoCvCommand(Guid EmpleadoId, string? Telefono) : IRequest<string?>;

public sealed class GuardarTelefonoCvCommandHandler : IRequestHandler<GuardarTelefonoCvCommand, string?>
{
    private readonly IEmpleadoRepository _empleados;

    public GuardarTelefonoCvCommandHandler(IEmpleadoRepository empleados) => _empleados = empleados;

    public async Task<string?> Handle(GuardarTelefonoCvCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        empleado.EditarTelefonoCv(request.Telefono);
        await _empleados.UpdateAsync(empleado, ct);
        return empleado.CvTelefono;
    }
}

internal static class ValidacionesExperienciaCv
{
    internal static void ValidarFechas(DateOnly desde, DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        if (desde > hoy || hasta > hoy)
            throw new ReglaDeNegocioException("Las fechas no pueden ser futuras.");
    }
}

/// <summary>Guarda las observaciones libres del CV del empleado.</summary>
public sealed record GuardarObservacionesCvCommand(Guid EmpleadoId, string? Observaciones) : IRequest<string?>;

public sealed class GuardarObservacionesCvCommandHandler : IRequestHandler<GuardarObservacionesCvCommand, string?>
{
    private readonly IEmpleadoRepository _empleados;

    public GuardarObservacionesCvCommandHandler(IEmpleadoRepository empleados) => _empleados = empleados;

    public async Task<string?> Handle(GuardarObservacionesCvCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        if (request.Observaciones is { Length: > 2000 })
            throw new ReglaDeNegocioException("Las observaciones no pueden superar los 2000 caracteres.");

        empleado.EditarObservacionesCv(request.Observaciones);
        await _empleados.UpdateAsync(empleado, ct);
        return empleado.CvObservaciones;
    }
}

/// <summary>Descarga del archivo de un certificado: el dueño o staff.</summary>
public sealed record DescargarCertificadoCvQuery(
    Guid CertificadoId, Guid EmpleadoIdSolicitante, IReadOnlyCollection<string> RolesSolicitante)
    : IRequest<(Stream Contenido, string NombreArchivo, string ContentType)>;

public sealed class DescargarCertificadoCvQueryHandler
    : IRequestHandler<DescargarCertificadoCvQuery, (Stream, string, string)>
{
    private static readonly IReadOnlySet<string> Staff =
        new HashSet<string> { "Responsable", "Rrhh", "Administrador", "Direccion" };

    private readonly ICertificadoCvRepository _certificados;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public DescargarCertificadoCvQueryHandler(ICertificadoCvRepository certificados, IAdjuntoRepository adjuntos,
        IFileStoragePort storage)
    {
        _certificados = certificados;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<(Stream, string, string)> Handle(DescargarCertificadoCvQuery request, CancellationToken ct)
    {
        var certificado = await _certificados.GetByIdAsync(request.CertificadoId, ct)
            ?? throw new EntidadNoEncontradaException("El certificado no existe.");

        var esStaff = request.RolesSolicitante.Any(Staff.Contains);
        if (!esStaff && certificado.EmpleadoId != request.EmpleadoIdSolicitante)
            throw new ReglaDeNegocioException("No podés ver un certificado de otro empleado.");

        var adjunto = await _adjuntos.GetByIdAsync(certificado.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El archivo del certificado no existe.");

        var contenido = await _storage.AbrirAsync(adjunto.StorageKey, ct);
        return (contenido, adjunto.NombreArchivo, adjunto.ContentType);
    }
}

/// <summary>Revisión de RRHH/Administración sobre un certificado (verificado u observado).</summary>
public sealed record RevisarCertificadoCvCommand(
    Guid CertificadoId, bool Verificado, string? Comentario) : IRequest<CertificadoCvAdminDto>;

public sealed class RevisarCertificadoCvCommandHandler : IRequestHandler<RevisarCertificadoCvCommand, CertificadoCvAdminDto>
{
    private readonly ICertificadoCvRepository _certificados;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAdjuntoRepository _adjuntos;

    public RevisarCertificadoCvCommandHandler(ICertificadoCvRepository certificados, IEmpleadoRepository empleados,
        IAdjuntoRepository adjuntos)
    {
        _certificados = certificados;
        _empleados = empleados;
        _adjuntos = adjuntos;
    }

    public async Task<CertificadoCvAdminDto> Handle(RevisarCertificadoCvCommand request, CancellationToken ct)
    {
        var certificado = await _certificados.GetByIdAsync(request.CertificadoId, ct)
            ?? throw new EntidadNoEncontradaException("El certificado no existe.");

        certificado.Revisar(request.Verificado, request.Comentario);
        await _certificados.UpdateAsync(certificado, ct);

        var empleado = await _empleados.GetByIdAsync(certificado.EmpleadoId, ct);
        var adjunto = await _adjuntos.GetByIdAsync(certificado.AdjuntoId, ct);

        return new CertificadoCvAdminDto(
            certificado.Id, certificado.EmpleadoId, empleado?.Legajo ?? 0,
            empleado is null ? "" : $"{empleado.Apellido}, {empleado.Nombre}", null,
            certificado.Nombre, certificado.Institucion, certificado.Tipo.ToString(), certificado.FechaObtencion,
            certificado.Estado.ToString(), certificado.ComentarioRevision, certificado.AdjuntoId,
            adjunto?.NombreArchivo ?? "", adjunto?.TamañoBytes ?? 0, certificado.FechaCarga);
    }
}

public sealed record AprobarTodosCertificadosCvCommand : IRequest<int>;

public sealed class AprobarTodosCertificadosCvCommandHandler : IRequestHandler<AprobarTodosCertificadosCvCommand, int>
{
    private readonly ICertificadoCvRepository _certificados;

    public AprobarTodosCertificadosCvCommandHandler(ICertificadoCvRepository certificados) => _certificados = certificados;

    public async Task<int> Handle(AprobarTodosCertificadosCvCommand request, CancellationToken ct)
    {
        var pendientes = (await _certificados.GetAllAsync(ct)).Where(c => c.Estado == EstadoCertificadoCv.Pendiente).ToList();
        foreach (var c in pendientes)
        {
            c.Revisar(true, null);
            await _certificados.UpdateAsync(c, ct);
        }
        return pendientes.Count;
    }
}

// ── Antecedentes académicos ────────────────────────────────────────────

public sealed record SubirAntecedenteAcademicoCommand(
    Guid EmpleadoId, string Titulo, string Institucion, string Nivel, string? Descripcion,
    DateOnly FechaDesde, DateOnly? FechaHasta,
    string ArchivoNombre, string ContentType, Stream Contenido) : IRequest<AntecedenteAcademicoDto>;

public sealed class SubirAntecedenteAcademicoCommandHandler : IRequestHandler<SubirAntecedenteAcademicoCommand, AntecedenteAcademicoDto>
{
    private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxBytes = 10 * 1024 * 1024;

    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICvAntecedenteAdjuntoRepository _adjuntosAnt;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public SubirAntecedenteAcademicoCommandHandler(ICvAntecedenteAcademicoRepository antecedentes,
        ICvAntecedenteAdjuntoRepository adjuntosAnt, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
        _adjuntosAnt = adjuntosAnt;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<AntecedenteAcademicoDto> Handle(SubirAntecedenteAcademicoCommand request, CancellationToken ct)
    {
        ValidacionesAntecedenteAcademico.ValidarFechas(request.FechaDesde, request.FechaHasta);
        if (request.Descripcion is { Length: > 3000 })
            throw new ReglaDeNegocioException("La descripción no puede superar los 3000 caracteres.");
        var nivel = ValidacionesAntecedenteAcademico.ParseNivel(request.Nivel);
        var extension = Path.GetExtension(request.ArchivoNombre).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            throw new ReglaDeNegocioException("Solo se permiten archivos PDF, JPG o PNG.");
        if (request.Contenido.Length > MaxBytes)
            throw new ReglaDeNegocioException("El archivo supera los 10 MB.");

        var adjuntoId = Guid.NewGuid();
        var storageKey = $"cv-antecedente/{adjuntoId}{extension}";
        await _storage.GuardarAsync(storageKey, request.ContentType, request.Contenido, ct);

        var adjunto = new Adjunto(request.ArchivoNombre, request.ContentType, request.Contenido.Length,
            storageKey, request.EmpleadoId);
        adjunto.VincularAEntidad("antecedente_academico", adjuntoId);
        await _adjuntos.AddAsync(adjunto, ct);

        var antecedente = new CvAntecedenteAcademico(request.EmpleadoId, request.Titulo, request.Institucion,
            nivel, request.Descripcion, request.FechaDesde, request.FechaHasta, adjunto.Id);
        await _antecedentes.AddAsync(antecedente, ct);

        var antAdj = new CvAntecedenteAdjunto(antecedente.Id, adjunto.Id);
        await _adjuntosAnt.AddAsync(antAdj, ct);

        var adjuntosDto = new List<ExperienciaAdjuntoDto>
        {
            new(antAdj.Id, adjunto.Id, adjunto.NombreArchivo, adjunto.ContentType, adjunto.TamañoBytes, antAdj.FechaCarga)
        };
        return new AntecedenteAcademicoDto(antecedente.Id, antecedente.Titulo, antecedente.Institucion,
            antecedente.Nivel.ToString(), antecedente.Descripcion, antecedente.FechaDesde, antecedente.FechaHasta,
            adjunto.Id, adjunto.NombreArchivo, adjunto.TamañoBytes, antecedente.FechaCarga, adjuntosDto);
    }
}

public sealed record ListarMisAntecedentesQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<AntecedenteAcademicoDto>>;

public sealed class ListarMisAntecedentesQueryHandler : IRequestHandler<ListarMisAntecedentesQuery, IReadOnlyList<AntecedenteAcademicoDto>>
{
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICvAntecedenteAdjuntoRepository _adjuntosAnt;
    private readonly IAdjuntoRepository _adjuntos;

    public ListarMisAntecedentesQueryHandler(ICvAntecedenteAcademicoRepository antecedentes,
        ICvAntecedenteAdjuntoRepository adjuntosAnt, IAdjuntoRepository adjuntos)
    {
        _antecedentes = antecedentes;
        _adjuntosAnt = adjuntosAnt;
        _adjuntos = adjuntos;
    }

    public async Task<IReadOnlyList<AntecedenteAcademicoDto>> Handle(ListarMisAntecedentesQuery request, CancellationToken ct)
    {
        var lista = (await _antecedentes.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(a => a.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today))
            .ThenByDescending(a => a.FechaDesde)
            .ToList();
        var resultado = new List<AntecedenteAcademicoDto>(lista.Count);
        foreach (var a in lista)
        {
            var adjuntosAnt = await _adjuntosAnt.GetByAntecedenteAsync(a.Id, ct);
            var adjuntosDto = new List<ExperienciaAdjuntoDto>(adjuntosAnt.Count);
            foreach (var aa in adjuntosAnt)
            {
                var adj = await _adjuntos.GetByIdAsync(aa.AdjuntoId, ct);
                if (adj is null) continue;
                adjuntosDto.Add(new ExperienciaAdjuntoDto(aa.Id, adj.Id, adj.NombreArchivo, adj.ContentType, adj.TamañoBytes, aa.FechaCarga));
            }
            var primero = adjuntosDto.FirstOrDefault();
            resultado.Add(new AntecedenteAcademicoDto(a.Id, a.Titulo, a.Institucion, a.Nivel.ToString(),
                a.Descripcion, a.FechaDesde, a.FechaHasta,
                primero?.AdjuntoId ?? a.AdjuntoId, primero?.NombreArchivo ?? "", primero?.TamanoBytes ?? 0, a.FechaCarga,
                adjuntosDto));
        }
        return resultado;
    }
}

public sealed record EditarAntecedenteAcademicoCommand(
    Guid EmpleadoId, Guid AntecedenteId, string Titulo, string Institucion, string Nivel,
    string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta) : IRequest<Unit>;

public sealed class EditarAntecedenteAcademicoCommandHandler : IRequestHandler<EditarAntecedenteAcademicoCommand, Unit>
{
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;

    public EditarAntecedenteAcademicoCommandHandler(ICvAntecedenteAcademicoRepository antecedentes) => _antecedentes = antecedentes;

    public async Task<Unit> Handle(EditarAntecedenteAcademicoCommand request, CancellationToken ct)
    {
        ValidacionesAntecedenteAcademico.ValidarFechas(request.FechaDesde, request.FechaHasta);
        if (request.Descripcion is { Length: > 3000 })
            throw new ReglaDeNegocioException("La descripción no puede superar los 3000 caracteres.");
        var nivel = ValidacionesAntecedenteAcademico.ParseNivel(request.Nivel);
        var ant = await _antecedentes.GetByIdAsync(request.AntecedenteId, ct)
            ?? throw new EntidadNoEncontradaException("El antecedente no existe.");
        if (ant.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés modificar un antecedente de otro empleado.");
        ant.Editar(request.Titulo, request.Institucion, nivel, request.Descripcion, request.FechaDesde, request.FechaHasta);
        await _antecedentes.UpdateAsync(ant, ct);
        return Unit.Value;
    }
}

public sealed record EliminarAntecedenteAcademicoCommand(Guid EmpleadoId, Guid AntecedenteId) : IRequest<Unit>;

public sealed class EliminarAntecedenteAcademicoCommandHandler : IRequestHandler<EliminarAntecedenteAcademicoCommand, Unit>
{
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICvAntecedenteAdjuntoRepository _adjuntosAnt;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarAntecedenteAcademicoCommandHandler(ICvAntecedenteAcademicoRepository antecedentes,
        ICvAntecedenteAdjuntoRepository adjuntosAnt, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
        _adjuntosAnt = adjuntosAnt;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarAntecedenteAcademicoCommand request, CancellationToken ct)
    {
        var ant = await _antecedentes.GetByIdAsync(request.AntecedenteId, ct)
            ?? throw new EntidadNoEncontradaException("El antecedente no existe.");
        if (ant.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar un antecedente de otro empleado.");
        var adjuntosAnt = await _adjuntosAnt.GetByAntecedenteAsync(ant.Id, ct);
        foreach (var aa in adjuntosAnt)
        {
            var adj = await _adjuntos.GetByIdAsync(aa.AdjuntoId, ct);
            if (adj is not null) try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
            await _adjuntosAnt.DeleteAsync(aa, ct);
        }
        await _antecedentes.DeleteAsync(ant, ct);
        return Unit.Value;
    }
}

public sealed record DescargarAntecedenteAcademicoQuery(
    Guid AntecedenteId, Guid EmpleadoIdSolicitante, IReadOnlyCollection<string> RolesSolicitante)
    : IRequest<(Stream Contenido, string NombreArchivo, string ContentType)>;

public sealed class DescargarAntecedenteAcademicoQueryHandler
    : IRequestHandler<DescargarAntecedenteAcademicoQuery, (Stream, string, string)>
{
    private static readonly IReadOnlySet<string> Staff =
        new HashSet<string> { "Responsable", "Rrhh", "Administrador", "Direccion" };

    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public DescargarAntecedenteAcademicoQueryHandler(ICvAntecedenteAcademicoRepository antecedentes,
        IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<(Stream, string, string)> Handle(DescargarAntecedenteAcademicoQuery request, CancellationToken ct)
    {
        var ant = await _antecedentes.GetByIdAsync(request.AntecedenteId, ct)
            ?? throw new EntidadNoEncontradaException("El antecedente no existe.");
        var esStaff = request.RolesSolicitante.Any(Staff.Contains);
        if (!esStaff && ant.EmpleadoId != request.EmpleadoIdSolicitante)
            throw new ReglaDeNegocioException("No podés ver un antecedente de otro empleado.");
        var adj = await _adjuntos.GetByIdAsync(ant.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El archivo del antecedente no existe.");
        var contenido = await _storage.AbrirAsync(adj.StorageKey, ct);
        return (contenido, adj.NombreArchivo, adj.ContentType);
    }
}

public sealed record SubirAntecedenteAdjuntoCommand(Guid EmpleadoId, Guid AntecedenteId, string ArchivoNombre, string ContentType, Stream Contenido) : IRequest<ExperienciaAdjuntoDto>;

public sealed class SubirAntecedenteAdjuntoCommandHandler : IRequestHandler<SubirAntecedenteAdjuntoCommand, ExperienciaAdjuntoDto>
{
    private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxBytes = 10 * 1024 * 1024;
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICvAntecedenteAdjuntoRepository _adjuntosAnt;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public SubirAntecedenteAdjuntoCommandHandler(ICvAntecedenteAcademicoRepository antecedentes,
        ICvAntecedenteAdjuntoRepository adjuntosAnt, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
        _adjuntosAnt = adjuntosAnt;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<ExperienciaAdjuntoDto> Handle(SubirAntecedenteAdjuntoCommand request, CancellationToken ct)
    {
        var antecedente = await _antecedentes.GetByIdAsync(request.AntecedenteId, ct)
            ?? throw new EntidadNoEncontradaException("El antecedente no existe.");
        if (antecedente.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés agregar anexos a un antecedente de otro empleado.");
        var existentes = await _adjuntosAnt.GetByAntecedenteAsync(request.AntecedenteId, ct);
        if (existentes.Count >= 5)
            throw new ReglaDeNegocioException("Máximo 5 anexos por antecedente.");
        var extension = Path.GetExtension(request.ArchivoNombre).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            throw new ReglaDeNegocioException("Solo se permiten archivos PDF, JPG o PNG.");
        if (request.Contenido.Length > MaxBytes)
            throw new ReglaDeNegocioException("El archivo supera los 10 MB.");
        var adjuntoId = Guid.NewGuid();
        var storageKey = $"cv-antecedente/{adjuntoId}{extension}";
        await _storage.GuardarAsync(storageKey, request.ContentType, request.Contenido, ct);
        var adjunto = new Adjunto(request.ArchivoNombre, request.ContentType, request.Contenido.Length, storageKey, request.EmpleadoId);
        adjunto.VincularAEntidad("antecedente_academico", adjuntoId);
        await _adjuntos.AddAsync(adjunto, ct);
        var antAdj = new CvAntecedenteAdjunto(request.AntecedenteId, adjunto.Id);
        await _adjuntosAnt.AddAsync(antAdj, ct);
        return new ExperienciaAdjuntoDto(antAdj.Id, adjunto.Id, adjunto.NombreArchivo, adjunto.ContentType, adjunto.TamañoBytes, antAdj.FechaCarga);
    }
}

public sealed record EliminarAntecedenteAdjuntoCommand(Guid EmpleadoId, Guid AntecedenteId, Guid AdjuntoId) : IRequest<Unit>;

public sealed class EliminarAntecedenteAdjuntoCommandHandler : IRequestHandler<EliminarAntecedenteAdjuntoCommand, Unit>
{
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICvAntecedenteAdjuntoRepository _adjuntosAnt;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarAntecedenteAdjuntoCommandHandler(ICvAntecedenteAcademicoRepository antecedentes,
        ICvAntecedenteAdjuntoRepository adjuntosAnt, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
        _adjuntosAnt = adjuntosAnt;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarAntecedenteAdjuntoCommand request, CancellationToken ct)
    {
        var antecedente = await _antecedentes.GetByIdAsync(request.AntecedenteId, ct)
            ?? throw new EntidadNoEncontradaException("El antecedente no existe.");
        if (antecedente.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar anexos de un antecedente de otro empleado.");
        var antAdj = await _adjuntosAnt.GetByIdAsync(request.AdjuntoId, ct)
            ?? await _adjuntosAnt.GetByAdjuntoIdAsync(request.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El anexo no existe.");
        if (antAdj.AntecedenteId != request.AntecedenteId)
            throw new EntidadNoEncontradaException("El anexo no pertenece a ese antecedente.");
        var adj = await _adjuntos.GetByIdAsync(antAdj.AdjuntoId, ct);
        if (adj is not null) try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
        await _adjuntosAnt.DeleteAsync(antAdj, ct);
        return Unit.Value;
    }
}

public sealed record DescargarAntecedenteAdjuntoQuery(Guid AntecedenteId, Guid AdjuntoId, Guid EmpleadoIdSolicitante, IReadOnlyCollection<string> RolesSolicitante) : IRequest<(Stream Contenido, string NombreArchivo, string ContentType)>;

public sealed class DescargarAntecedenteAdjuntoQueryHandler : IRequestHandler<DescargarAntecedenteAdjuntoQuery, (Stream, string, string)>
{
    private static readonly IReadOnlySet<string> Staff =
        new HashSet<string> { "Responsable", "Rrhh", "Administrador", "Direccion" };
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly ICvAntecedenteAdjuntoRepository _adjuntosAnt;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public DescargarAntecedenteAdjuntoQueryHandler(ICvAntecedenteAcademicoRepository antecedentes,
        ICvAntecedenteAdjuntoRepository adjuntosAnt, IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
        _adjuntosAnt = adjuntosAnt;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<(Stream, string, string)> Handle(DescargarAntecedenteAdjuntoQuery request, CancellationToken ct)
    {
        var ant = await _antecedentes.GetByIdAsync(request.AntecedenteId, ct)
            ?? throw new EntidadNoEncontradaException("El antecedente no existe.");
        var esStaff = request.RolesSolicitante.Any(Staff.Contains);
        if (!esStaff && ant.EmpleadoId != request.EmpleadoIdSolicitante)
            throw new ReglaDeNegocioException("No podés ver anexos de un antecedente de otro empleado.");
        var antAdj = await _adjuntosAnt.GetByIdAsync(request.AdjuntoId, ct)
            ?? await _adjuntosAnt.GetByAdjuntoIdAsync(request.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El anexo no existe.");
        if (antAdj.AntecedenteId != request.AntecedenteId)
            throw new EntidadNoEncontradaException("El anexo no pertenece a ese antecedente.");
        var adj = await _adjuntos.GetByIdAsync(antAdj.AdjuntoId, ct)
            ?? throw new EntidadNoEncontradaException("El archivo no existe.");
        var contenido = await _storage.AbrirAsync(adj.StorageKey, ct);
        return (contenido, adj.NombreArchivo, adj.ContentType);
    }
}

internal static class ValidacionesAntecedenteAcademico
{
    internal static void ValidarFechas(DateOnly desde, DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        if (desde > hoy || hasta > hoy)
            throw new ReglaDeNegocioException("Las fechas no pueden ser futuras.");
    }

    internal static NivelAcademico ParseNivel(string nivel) => nivel.Trim().ToLowerInvariant() switch
    {
        "secundario" => NivelAcademico.Secundario,
        "terciario" => NivelAcademico.Terciario,
        "universitario" => NivelAcademico.Universitario,
        "posgrado" => NivelAcademico.Posgrado,
        "maestria" or "maestría" => NivelAcademico.Maestria,
        "doctorado" => NivelAcademico.Doctorado,
        "otro" => NivelAcademico.Otro,
        _ => throw new ReglaDeNegocioException("El nivel académico no es válido.")
    };
}
