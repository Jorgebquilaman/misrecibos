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
    private readonly IAreaRepository _areas;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public GenerarMiCvQueryHandler(IEmpleadoRepository empleados, ICertificadoCvRepository certificados,
        ICvExperienciaRepository experiencias, ICvAntecedenteAcademicoRepository antecedentes,
        IAreaRepository areas, IAdjuntoRepository adjuntos,
        IFileStoragePort storage)
    {
        _empleados = empleados;
        _certificados = certificados;
        _experiencias = experiencias;
        _antecedentes = antecedentes;
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
            archivos.Add(new ArchivoCertificadoCv(adjunto.NombreArchivo, adjunto.ContentType, ms.ToArray()));
        }
        foreach (var ant in (await _antecedentes.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(a => a.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today))
            .ThenByDescending(a => a.FechaDesde))
        {
            var adjunto = await _adjuntos.GetByIdAsync(ant.AdjuntoId, ct);
            if (adjunto is null) continue;
            await using var stream = await _storage.AbrirAsync(adjunto.StorageKey, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            archivos.Add(new ArchivoCertificadoCv(adjunto.NombreArchivo, adjunto.ContentType, ms.ToArray()));
        }

        return new CvCompletoDto(
            new DatosCvPdf(
                new DatosCvEmpleado(empleado.NombreCompleto, empleado.Legajo, empleado.Correo.Valor,
                    empleado.Dni, area, empleado.CvObservaciones, empleado.CvTelefono),
                certificados,
                experiencias,
                antecedentes),
            archivos);
    }
}

public sealed record ExperienciaCvDto(Guid Id, string Puesto, string Institucion, string? Descripcion,
    DateOnly FechaDesde, DateOnly? FechaHasta);

public sealed record AntecedenteAcademicoDto(Guid Id, string Titulo, string Institucion, string Nivel,
    string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta,
    Guid AdjuntoId, string NombreArchivo, long TamanoBytes, DateTime FechaCarga);

/// <summary>Lista las experiencias laborales declaradas por el propio empleado.</summary>
public sealed record ListarMisExperienciasQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<ExperienciaCvDto>>;

public sealed class ListarMisExperienciasQueryHandler : IRequestHandler<ListarMisExperienciasQuery, IReadOnlyList<ExperienciaCvDto>>
{
    private readonly ICvExperienciaRepository _experiencias;

    public ListarMisExperienciasQueryHandler(ICvExperienciaRepository experiencias) => _experiencias = experiencias;

    public async Task<IReadOnlyList<ExperienciaCvDto>> Handle(ListarMisExperienciasQuery request, CancellationToken ct) =>
        (await _experiencias.GetByEmpleadoAsync(request.EmpleadoId, ct))
            .OrderByDescending(e => e.FechaHasta ?? DateOnly.FromDateTime(DateTime.Today))
            .ThenByDescending(e => e.FechaDesde)
            .Select(e => new ExperienciaCvDto(e.Id, e.Puesto, e.Institucion, e.Descripcion, e.FechaDesde, e.FechaHasta))
            .ToList();
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
            experiencia.Descripcion, experiencia.FechaDesde, experiencia.FechaHasta);
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

    public EliminarExperienciaCvCommandHandler(ICvExperienciaRepository experiencias) => _experiencias = experiencias;

    public async Task<Unit> Handle(EliminarExperienciaCvCommand request, CancellationToken ct)
    {
        var experiencia = await _experiencias.GetByIdAsync(request.ExperienciaId, ct)
            ?? throw new EntidadNoEncontradaException("La experiencia no existe.");
        if (experiencia.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar una experiencia de otro empleado.");

        await _experiencias.DeleteAsync(experiencia, ct);
        return Unit.Value;
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
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public SubirAntecedenteAcademicoCommandHandler(ICvAntecedenteAcademicoRepository antecedentes,
        IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
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

        return new AntecedenteAcademicoDto(antecedente.Id, antecedente.Titulo, antecedente.Institucion,
            antecedente.Nivel.ToString(), antecedente.Descripcion, antecedente.FechaDesde, antecedente.FechaHasta,
            adjunto.Id, adjunto.NombreArchivo, adjunto.TamañoBytes, antecedente.FechaCarga);
    }
}

public sealed record ListarMisAntecedentesQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<AntecedenteAcademicoDto>>;

public sealed class ListarMisAntecedentesQueryHandler : IRequestHandler<ListarMisAntecedentesQuery, IReadOnlyList<AntecedenteAcademicoDto>>
{
    private readonly ICvAntecedenteAcademicoRepository _antecedentes;
    private readonly IAdjuntoRepository _adjuntos;

    public ListarMisAntecedentesQueryHandler(ICvAntecedenteAcademicoRepository antecedentes, IAdjuntoRepository adjuntos)
    {
        _antecedentes = antecedentes;
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
            var adj = await _adjuntos.GetByIdAsync(a.AdjuntoId, ct);
            resultado.Add(new AntecedenteAcademicoDto(a.Id, a.Titulo, a.Institucion, a.Nivel.ToString(),
                a.Descripcion, a.FechaDesde, a.FechaHasta, a.AdjuntoId,
                adj?.NombreArchivo ?? "", adj?.TamañoBytes ?? 0, a.FechaCarga));
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
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IFileStoragePort _storage;

    public EliminarAntecedenteAcademicoCommandHandler(ICvAntecedenteAcademicoRepository antecedentes,
        IAdjuntoRepository adjuntos, IFileStoragePort storage)
    {
        _antecedentes = antecedentes;
        _adjuntos = adjuntos;
        _storage = storage;
    }

    public async Task<Unit> Handle(EliminarAntecedenteAcademicoCommand request, CancellationToken ct)
    {
        var ant = await _antecedentes.GetByIdAsync(request.AntecedenteId, ct)
            ?? throw new EntidadNoEncontradaException("El antecedente no existe.");
        if (ant.EmpleadoId != request.EmpleadoId)
            throw new ReglaDeNegocioException("No podés eliminar un antecedente de otro empleado.");
        var adj = await _adjuntos.GetByIdAsync(ant.AdjuntoId, ct);
        if (adj is not null)
        {
            try { await _storage.EliminarAsync(adj.StorageKey, ct); } catch { }
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
