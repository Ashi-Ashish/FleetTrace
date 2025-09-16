using System.Text.Json;
using FleetTrace.Shared.Contracts;

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };

var evt = new TelematicsEvent
{
    EventId   = Guid.NewGuid().ToString(),
    VehicleId = "veh-001",
    Ts        = DateTime.UtcNow,
    Lat       = 43.4516,
    Lon       = -80.4925
};

Console.WriteLine(JsonSerializer.Serialize(evt, jsonOptions));