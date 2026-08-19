using System.Text.RegularExpressions;

namespace PortalIUPA.Domain.ValueObjects;

public sealed record Email
{
    public const string DominioInstitucional = "iupa.edu.ar";

    private static readonly Regex Patron =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Valor { get; }

    public Email(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("El correo no puede estar vacío.", nameof(valor));

        var normalizado = valor.Trim().ToLowerInvariant();
        if (!Patron.IsMatch(normalizado))
            throw new ArgumentException("El correo no tiene un formato válido.", nameof(valor));

        Valor = normalizado;
    }

    public bool EsInstitucional => Valor.EndsWith($"@{DominioInstitucional}", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Valor;

    public static implicit operator string(Email email) => email.Valor;
    public static explicit operator Email(string valor) => new(valor);
}