using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.DomainServices;

public sealed record ResultadoValidacion(bool EsValido, string? Error)
{
    public static ResultadoValidacion Ok() => new(true, null);
    public static ResultadoValidacion Fallo(string error) => new(false, error);
}

/// <summary>
/// Valida los límites mensuales y anuales de un tipo de licencia contra el consumo ya registrado
/// (solicitudes aprobadas + en espera). Regla nueva que el legacy no implementaba del todo.
/// </summary>
public static class ValidadorLimitesLicencia
{
    public static ResultadoValidacion Validar(TipoLicencia tipo, int diasSolicitadosEnMes, int diasSolicitadosEnAnio,
        int diasConsumidosEnMes, int diasConsumidosEnAnio)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        if (diasSolicitadosEnMes <= 0 || diasSolicitadosEnAnio <= 0)
            return ResultadoValidacion.Fallo("El rango de fechas solicitado es inválido.");

        if (tipo.LimiteMensual is { } limiteMensual &&
            diasConsumidosEnMes + diasSolicitadosEnMes > limiteMensual)
            return ResultadoValidacion.Fallo(
                $"La licencia supera el límite mensual de {limiteMensual} días (ya utilizados: {diasConsumidosEnMes}).");

        if (tipo.LimiteAnual is { } limiteAnual &&
            diasConsumidosEnAnio + diasSolicitadosEnAnio > limiteAnual)
            return ResultadoValidacion.Fallo(
                $"La licencia supera el límite anual de {limiteAnual} días (ya utilizados: {diasConsumidosEnAnio}).");

        return ResultadoValidacion.Ok();
    }
}