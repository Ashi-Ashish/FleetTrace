using System.Text.Json.Serialization;

namespace FleetTrace.Shared.Contracts;

public sealed class TelematicsEvent
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "1.0.0";

    [JsonPropertyName("event_id")]
    public required string EventId { get; init; }

    [JsonPropertyName("vehicle_id")]
    public required string VehicleId { get; init; }

    [JsonPropertyName("driver_id")]
    public string? DriverId { get; init; }

    [JsonPropertyName("ts")]
    public required DateTime Ts { get; init; } // UTC

    [JsonPropertyName("lat")]
    public required double Lat { get; init; }

    [JsonPropertyName("lon")]
    public required double Lon { get; init; }

    [JsonPropertyName("speed_kph")]
    public double? SpeedKph { get; init; }

    [JsonPropertyName("heading_deg")]
    public double? HeadingDeg { get; init; }

    [JsonPropertyName("accel_ms2")]
    public double? AccelMs2 { get; init; }

    [JsonPropertyName("fuel_pct")]
    public double? FuelPct { get; init; }

    [JsonPropertyName("odometer_km")]
    public double? OdometerKm { get; init; }

    [JsonPropertyName("meta")]
    public Meta? Meta { get; init; }
}

public sealed class Meta
{
    [JsonPropertyName("route_id")]
    public string? RouteId { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; } // "simulator" | "device" | "replay"
}