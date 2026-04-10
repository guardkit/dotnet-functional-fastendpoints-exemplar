using System.Text.Json;
using Exemplar.Fleet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Exemplar.Fleet.Services;

/// <summary>
/// Publishes domain events to NATS JetStream using the <see cref="MessageEnvelope"/> wire format.
/// JetStream requires a stream to be configured for the target subject. If no stream exists or
/// the broker is unavailable, the error is logged and swallowed so the calling operation is
/// never failed by event publishing.
/// Falls back to Core NATS publish when <see cref="FleetOptions.JetStreamEnabled"/> is false.
/// </summary>
public sealed class NatsEventPublisher : INatsEventPublisher
{
    private readonly NatsConnection _conn;
    private readonly FleetOptions _opts;
    private readonly ILogger<NatsEventPublisher> _logger;

    // Lazily created — avoids allocating a JS context until the first publish.
    private NatsJSContext? _js;
    private NatsJSContext JsContext => _js ??= new NatsJSContext(_conn);

    public NatsEventPublisher(
        NatsConnection conn,
        IOptions<FleetOptions> opts,
        ILogger<NatsEventPublisher> logger)
    {
        _conn = conn;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        string subject,
        EventType eventType,
        object payload,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        var envelope = new MessageEnvelope
        {
            SourceId = _opts.SourceId,
            EventType = eventType,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToElement(payload),
        };

        try
        {
            if (_opts.JetStreamEnabled)
                await JsContext.PublishAsync(subject, envelope, cancellationToken: ct);
            else
                await _conn.PublishAsync(subject, envelope, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Event publishing is best-effort — log and swallow so the main operation succeeds.
            _logger.LogWarning(ex,
                "Failed to publish {EventType} event to {Subject}", eventType, subject);
        }
    }
}
