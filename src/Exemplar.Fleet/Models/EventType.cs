using System.Text.Json.Serialization;

namespace Exemplar.Fleet.Models;

/// <summary>
/// Wire-format event types — must match Python nats-core EventType string values exactly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EventType>))]
public enum EventType
{
    [JsonStringEnumMemberName("register")]
    Register,

    [JsonStringEnumMemberName("deregister")]
    Deregister,

    [JsonStringEnumMemberName("heartbeat")]
    Heartbeat,

    [JsonStringEnumMemberName("command")]
    Command,

    [JsonStringEnumMemberName("result")]
    Result,

    [JsonStringEnumMemberName("event")]
    Event,
}
