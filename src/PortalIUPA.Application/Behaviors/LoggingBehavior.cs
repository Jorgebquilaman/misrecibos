using MediatR;
using Microsoft.Extensions.Logging;

namespace PortalIUPA.Application.Behaviors;

/// <summary>Log estructurado de cada request/response de los casos de uso.</summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var nombre = typeof(TRequest).Name;
        _logger.LogInformation("Inicio {Request}", nombre);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var respuesta = await next();
            stopwatch.Stop();
            _logger.LogInformation("Fin {Request} en {Ms} ms", nombre, stopwatch.ElapsedMilliseconds);
            return respuesta;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error en {Request} a los {Ms} ms", nombre, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}