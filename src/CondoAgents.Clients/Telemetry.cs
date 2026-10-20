using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CondoAgents.Clients;

public static class Telemetry
{
    public const string ServiceName = "CondoAgents";

    public static readonly ActivitySource Source = new(ServiceName);

    public static readonly Meter Meter = new(ServiceName);
}