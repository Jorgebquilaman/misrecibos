namespace PortalIUPA.Application.Common;

/// <summary>Entidad solicitada no encontrada (404).</summary>
public sealed class EntidadNoEncontradaException : Exception
{
    public EntidadNoEncontradaException(string mensaje) : base(mensaje) { }
}

/// <summary>Violación de una regla de negocio o validación (400).</summary>
public sealed class ReglaDeNegocioException : Exception
{
    public ReglaDeNegocioException(string mensaje) : base(mensaje) { }
}

/// <summary>El usuario no está habilitado para la operación (403).</summary>
public sealed class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(string mensaje = "No tiene permisos para realizar esta operación.") : base(mensaje) { }
}