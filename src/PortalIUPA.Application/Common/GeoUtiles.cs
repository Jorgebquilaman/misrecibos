namespace PortalIUPA.Application.Common;

/// <summary>Cálculos geográficos para la detección de edificios en marcas manuales.</summary>
public static class GeoUtiles
{
    /// <summary>Distancia en metros entre dos coordenadas (fórmula de Haversine).</summary>
    public static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        const double RadioTierraM = 6_371_000;
        var dLat = A_Radianes(lat2 - lat1);
        var dLon = A_Radianes(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(A_Radianes(lat1)) * Math.Cos(A_Radianes(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RadioTierraM * Math.Asin(Math.Sqrt(a));
    }

    private static double A_Radianes(double grados) => grados * Math.PI / 180.0;
}
