using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PortalIUPA.Application.Common;

namespace PortalIUPA.Api.Middlewares;

/// <summary>Convierte excepciones de dominio/aplicación en respuestas HTTP limpias (400/403/404/500).</summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (EntidadNoEncontradaException ex)
        {
            await ResponderAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (AccesoDenegadoException ex)
        {
            await ResponderAsync(context, HttpStatusCode.Forbidden, ex.Message);
        }
        catch (ReglaDeNegocioException ex)
        {
            await ResponderAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (RelojNoDisponibleException ex)
        {
            if (ex.ReintentoEn is { } retry)
                context.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
            await ResponderAsync(context, HttpStatusCode.ServiceUnavailable, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Ruta}", context.Request.Path);
            // Incluimos el mensaje de la excepción para que el usuario pueda reportar el error real.
            await ResponderAsync(context, HttpStatusCode.InternalServerError,
                $"Ocurrió un error inesperado: {ex.Message}");
        }
    }

    private static async Task ResponderAsync(HttpContext context, HttpStatusCode codigo, string mensaje)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = (int)codigo;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = mensaje }));
    }
}