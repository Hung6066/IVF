using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// GeoIP location service — resolves IP addresses to geographic coordinates.
/// Uses a lightweight lookup for common patterns; production should integrate MaxMind GeoIP2.
/// Implements Haversine formula for distance calculations (impossible travel detection).
/// </summary>
public sealed class GeoLocationService(
    ILogger<GeoLocationService> logger) : IGeoLocationService
{
    public GeoLocation? Resolve(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return null;

        // Private/loopback IPs → local
        if (ipAddress.StartsWith("127.") || ipAddress == "::1" ||
            ipAddress.StartsWith("10.") || ipAddress.StartsWith("192.168.") ||
            ipAddress.StartsWith("172.16.") || ipAddress.StartsWith("172.17.") ||
            ipAddress.StartsWith("172.18.") || ipAddress.StartsWith("172.19.") ||
            ipAddress.StartsWith("172.2") || ipAddress.StartsWith("172.3"))
        {
            return new GeoLocation("Local", "LO", null, "Localhost", 0, 0, "Local Network");
        }

        // For production: integrate MaxMind GeoIP2 or similar
        // This stub returns null for unknown IPs — the system degrades gracefully
        logger.LogDebug("GeoIP lookup for {IP} — returning null (no GeoIP database configured)", ipAddress);
        return null;
    }

    /// <summary>
    /// Haversine formula — calculates the great-circle distance between two points on Earth.
    /// Used for impossible travel detection.
    /// </summary>
    public double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in km

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
