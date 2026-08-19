using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;

namespace PortalIUPA.Application.Behaviors;

/// <summary>Ejecuta los validadores FluentValidation registrados antes de cada handler.</summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validadores = _validators.ToList();
        if (validadores.Count == 0)
            return await next();

        var contexto = new ValidationContext<TRequest>(request);
        var errores = new List<string>();

        foreach (var validador in validadores)
        {
            var resultado = await validador.ValidateAsync(contexto, cancellationToken);
            errores.AddRange(resultado.Errors.Select(e => e.ErrorMessage));
        }

        if (errores.Count > 0)
            throw new ReglaDeNegocioException(string.Join(" | ", errores));

        return await next();
    }
}