using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Json.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace StreamWorker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly IConfiguration _cfg;

    // Kafka
    private IConsumer<string, string>? _consumer;
    private IProducer<string, string>? _producer;

    // Settings
    private readonly string _bootstrap;
    private readonly string _inputTopic;
    private readonly string _dlqTopic;
    private readonly string _groupId;
    private readonly string _schemaPath;

    // JSON
    private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = null };

    // JSON Schema
    private JsonSchema? _schema;
    private readonly EvaluationOptions _eval = new()
    {
        OutputFormat = OutputFormat.Hierarchical,
        RequireFormatValidation = true
    };

    public Worker(ILogger<Worker> log, IConfiguration cfg)
    {
        _log = log;
        _cfg = cfg;

        _bootstrap  = _cfg["Worker:BootstrapServers"] ?? "localhost:9092";
        _inputTopic = _cfg["Worker:InputTopic"]       ?? "telematics.events";
        _dlqTopic   = _cfg["Worker:DlqTopic"]         ?? "telematics.dlq";
        _groupId    = _cfg["Worker:GroupId"]          ?? "fleettrace-worker";
        _schemaPath = _cfg["Worker:SchemaPath"]       ?? "../../contracts/telematics_event.schema.json";
    }

    public override Task StartAsync(CancellationToken ct)
    {
        // Load schema once
        if (!File.Exists(_schemaPath))
            throw new FileNotFoundException($"Schema not found at '{_schemaPath}'");

        var schemaText = File.ReadAllText(_schemaPath);
        _schema = JsonSchema.FromText(schemaText);

        // Build Kafka client pair
        var cCfg = new ConsumerConfig
        {
            BootstrapServers = _bootstrap,
            GroupId          = _groupId,
            EnableAutoCommit = false,
            AutoOffsetReset  = AutoOffsetReset.Earliest
        };

        var pCfg = new ProducerConfig
        {
            BootstrapServers = _bootstrap,
            EnableIdempotence = true
        };

        _consumer = new ConsumerBuilder<string, string>(cCfg).Build();
        _producer = new ProducerBuilder<string, string>(pCfg).Build();

        _consumer.Subscribe(_inputTopic);

        _log.LogInformation("StreamWorker starting. InputTopic={Input} DLQ={Dlq} Schema={Schema}",
            _inputTopic, _dlqTopic, Path.GetFullPath(_schemaPath));

        return base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (_schema is null) throw new InvalidOperationException("Schema not loaded.");
        if (_consumer is null || _producer is null) throw new InvalidOperationException("Kafka not initialized.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cr = _consumer.Consume(TimeSpan.FromMilliseconds(250));
                if (cr is null) continue;

                var key = cr.Message.Key ?? string.Empty;
                var raw = cr.Message.Value ?? string.Empty;

                // 1) Validate against JSON Schema
                var node = JsonNode.Parse(raw);
                var result = _schema.Evaluate(node!, _eval);

                if (!result.IsValid)
                {
                    // Build DLQ envelope with reasons
                    var errors = result.Details
                        .Where(d => d.HasErrors)
                        .SelectMany(d => d.Errors!)
                        .Select(e => new { path = e.Key, message = e.Value })
                        .ToArray();

                    var dlqEnvelope = new
                    {
                        reason = "schema_validation_failed",
                        errors,
                        original = node
                    };

                    await _producer.ProduceAsync(_dlqTopic, new Message<string, string>
                    {
                        Key = key,
                        Value = JsonSerializer.Serialize(dlqEnvelope, _json)
                    }, ct);

                    _log.LogWarning("DLQ sent for key={Key} errors={Count}", key, errors.Length);
                    _consumer.Commit(cr);
                    continue;
                }

                // 2) Deserialize to DTO for downstream pipeline (you'll add enrichment next)
                // NOTE: The DTO type lives in Shared.Contracts; reference already added.
                var evt = JsonSerializer.Deserialize<FleetTrace.Shared.Contracts.TelematicsEvent>(raw, _json);
                if (evt is null)
                {
                    await SendExceptionToDlqAsync(key, "deserialization_returned_null", raw, ct);
                    _consumer.Commit(cr);
                    continue;
                }

                // TODO: enrichment + storage goes here
                _log.LogInformation("Accepted event {EventId} veh={Veh} ts={Ts}", evt.EventId, evt.VehicleId, evt.Ts);

                _consumer.Commit(cr);
            }
            catch (ConsumeException kex)
            {
                _log.LogError(kex, "Kafka consume error: {Reason}", kex.Error.Reason);
                // brief pause to avoid tight loop
                await Task.Delay(200, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled worker exception");
                // best-effort: try to send to DLQ if we still have producer
                // (no original record available here unless you capture it)
                await Task.Delay(200, ct);
            }
        }
    }

    private async Task SendExceptionToDlqAsync(string key, string reason, string original, CancellationToken ct)
    {
        if (_producer is null) return;
        var payload = JsonSerializer.Serialize(new { reason, original }, _json);
        await _producer.ProduceAsync(_dlqTopic, new Message<string, string> { Key = key, Value = payload }, ct);
    }

    public override Task StopAsync(CancellationToken ct)
    {
        _log.LogInformation("StreamWorker stopping...");
        _consumer?.Close();
        _consumer?.Dispose();
        _producer?.Flush(TimeSpan.FromSeconds(2));
        _producer?.Dispose();
        return base.StopAsync(ct);
    }
}