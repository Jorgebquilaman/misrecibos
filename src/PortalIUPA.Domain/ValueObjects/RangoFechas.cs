namespace PortalIUPA.Domain.ValueObjects;

public sealed record RangoFechas
{
    public DateOnly Inicio { get; }
    public DateOnly Fin { get; }

    public RangoFechas(DateOnly inicio, DateOnly fin)
    {
        if (fin < inicio)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fin));

        Inicio = inicio;
        Fin = fin;
    }

    public int Dias => Fin.DayNumber - Inicio.DayNumber + 1;

    public bool SolapaCon(RangoFechas otro) =>
        Inicio <= otro.Fin && otro.Inicio <= Fin;

    public int DiasDentroDelMes(int anio, int mes) =>
        DiasDentroDe(new DateOnly(anio, mes, 1), new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes)));

    public int DiasDentroDelAnio(int anio) =>
        DiasDentroDe(new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31));

    private int DiasDentroDe(DateOnly desde, DateOnly hasta)
    {
        if (Fin < desde || Inicio > hasta)
            return 0;

        var desdeEfectivo = Inicio > desde ? Inicio : desde;
        var hastaEfectivo = Fin < hasta ? Fin : hasta;
        return hastaEfectivo.DayNumber - desdeEfectivo.DayNumber + 1;
    }
}