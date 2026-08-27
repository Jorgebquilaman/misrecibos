using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Ports;

/// <summary>Marca cruda leída de la base externa del checador biométrico (solo lectura).</summary>
public sealed record MarcaRelojCruda(int Legajo, DateTime FechaHora, TipoMarca Tipo, string? Origen);

/// <summary>
/// Adaptador de solo lectura hacia la base del reloj (SQL Server del checador o fuente simulada).
/// Los datos se leen en vivo en cada consulta, sin copias intermedias.
/// </summary>
public interface IRelojDataSource
{
    /// <summary>Devuelve las marcas en el rango [desde, hasta]; si <paramref name="legajo"/> no es null, solo las de ese legajo.</summary>
    Task<IReadOnlyList<MarcaRelojCruda>> ObtenerMarcasAsync(int? legajo, DateTime desde, DateTime hasta,
        CancellationToken ct = default);

    /// <summary>Registra una marca manual directamente en la base del reloj (SQL Server). Retorna false si no se pudo registrar.</summary>
    Task<bool> RegistrarMarcaAsync(int legajo, DateTime fechaHora, TipoMarca tipo, CancellationToken ct = default);

    /// <summary>Edita una marca manual en la base del reloj (localiza por valores anteriores). Retorna false si no se pudo editar.</summary>
    Task<bool> EditarMarcaAsync(int legajo, DateTime fechaHoraVieja, TipoMarca tipoViejo,
        DateTime fechaHoraNueva, TipoMarca tipoNuevo, CancellationToken ct = default);

    /// <summary>Elimina una marca manual en la base del reloj (localiza por valores). Retorna false si no se pudo eliminar.</summary>
    Task<bool> EliminarMarcaAsync(int legajo, DateTime fechaHora, TipoMarca tipo, CancellationToken ct = default);
}