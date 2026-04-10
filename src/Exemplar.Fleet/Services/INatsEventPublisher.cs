using Exemplar.Fleet.Models;

namespace Exemplar.Fleet.Services;

/// <summary>
/// Publishes domain events from .NET bounded contexts to NATS JetStream
/// using the <see cref="MessageEnvelope"/> wire format.
/// Implementations must swallow publish errors so that event publishing
/// never causes the main operation to fail.
/// </summary>
public interface INatsEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="payload"/> to <paramref name="subject"/> on JetStream.
    /// Fire-and-forget: failures are logged and swallowed.
    /// </summary>
    Task PublishAsync(
        string subject,
        EventType eventType,
        object payload,
        string? correlationId = null,
        CancellationToken ct = default);
}
