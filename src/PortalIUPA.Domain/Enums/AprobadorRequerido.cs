namespace PortalIUPA.Domain.Enums;

/// <summary>Quién debe aprobar un nivel de la cadena de aprobación de un tipo de licencia.</summary>
public enum AprobadorRequerido
{
    /// <summary>El responsable directo del solicitante, según la relación "a cargo" vigente.</summary>
    ResponsableDirecto = 1,
    Responsable = 2,
    Rrhh = 3,
    Direccion = 4,
    Administrador = 5,
}