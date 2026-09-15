using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PSScriptWebApp.Models;

namespace PSScriptWebApp.Services;

public sealed class AuditLoggingOptions
{
    public string Directory { get; set; } = "audit";
}

public sealed record ExecutionAuditContext(string Source, string Actor);

public interface IExecutionAuditContextAccessor
{
    ExecutionAuditContext? Current { get; }
    IDisposable Push(ExecutionAuditContext context);
}

public sealed class ExecutionAuditContextAccessor : IExecutionAuditContextAccessor
{
    private static readonly AsyncLocal<ExecutionAuditContext?> CurrentContext = new();

    public ExecutionAuditContext? Current => CurrentContext.Value;

    public IDisposable Push(ExecutionAuditContext context)
    {
        var previous = CurrentContext.Value;
        CurrentContext.Value = context;
        return new ContextScope(() => CurrentContext.Value = previous);
    }

    private sealed class ContextScope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

public interface IExecutionAuditService
{
    Task WriteAsync(ExecutionAuditRecord record, CancellationToken cancellationToken = default);
}

public sealed class JsonLinesExecutionAuditService : IExecutionAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private readonly string _directory;

    public JsonLinesExecutionAuditService(IOptions<AuditLoggingOptions> options, IWebHostEnvironment environment)
    {
        _directory = Path.IsPathRooted(options.Value.Directory)
            ? options.Value.Directory
            : Path.Combine(environment.ContentRootPath, options.Value.Directory);
    }

    public async Task WriteAsync(ExecutionAuditRecord record, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"useradmin-audit-{record.TimestampUtc:yyyy-MM-dd}.jsonl");
        var line = JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine;

        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(path, line, cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }
}

public sealed class AuditingPowerShellService : IPowerShellService
{
    private readonly IPowerShellService _inner;
    private readonly IExecutionAuditService _audit;
    private readonly IExecutionAuditContextAccessor _context;

    public AuditingPowerShellService(IPowerShellService inner, IExecutionAuditService audit, IExecutionAuditContextAccessor context)
    {
        _inner = inner;
        _audit = audit;
        _context = context;
    }

    public List<PowerShellScript> GetAvailableScripts() => _inner.GetAvailableScripts();

    public PowerShellScript GetScriptDetails(string scriptName) => _inner.GetScriptDetails(scriptName);

    public Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, string> parameters) =>
        ExecuteAuditedAsync(scriptName, parameters, () => _inner.ExecuteScriptAsync(scriptName, parameters));

    public Task StreamScriptOutputAsync(string scriptName, Dictionary<string, string> parameters, Func<string, Task> onLine, CancellationToken cancellationToken) =>
        StreamAuditedAsync(scriptName, parameters, onLine, cancellationToken);

    private async Task<ScriptExecutionResult> ExecuteAuditedAsync(string scriptName, Dictionary<string, string> parameters, Func<Task<ScriptExecutionResult>> execute)
    {
        var audit = CreateRecord(scriptName, "Execute", parameters);
        var stopwatch = Stopwatch.StartNew();
        await _audit.WriteAsync(Clone(audit));

        try
        {
            var result = await execute();
            audit.Outcome = result.Success ? "Succeeded" : "Failed";
            return result;
        }
        catch
        {
            audit.Outcome = "Failed";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            audit.DurationMs = stopwatch.ElapsedMilliseconds;
            await _audit.WriteAsync(Clone(audit));
        }
    }

    private async Task StreamAuditedAsync(string scriptName, Dictionary<string, string> parameters, Func<string, Task> onLine, CancellationToken cancellationToken)
    {
        var audit = CreateRecord(scriptName, "Stream", parameters);
        var stopwatch = Stopwatch.StartNew();
        await _audit.WriteAsync(Clone(audit));
        bool? streamedSuccess = null;

        try
        {
            async Task ForwardStreamEventAsync(string line)
            {
                if (TryReadDoneSuccess(line, out var success))
                {
                    streamedSuccess = success;
                }

                await onLine(line);
            }

            await _inner.StreamScriptOutputAsync(scriptName, parameters, ForwardStreamEventAsync, cancellationToken);
            audit.Outcome = streamedSuccess == true ? "Succeeded" : "Failed";
        }
        catch (OperationCanceledException)
        {
            audit.Outcome = "Cancelled";
            throw;
        }
        catch
        {
            audit.Outcome = "Failed";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            audit.DurationMs = stopwatch.ElapsedMilliseconds;
            await _audit.WriteAsync(Clone(audit));
        }
    }

    private ExecutionAuditRecord CreateRecord(string scriptName, string mode, Dictionary<string, string> parameters)
    {
        var context = _context.Current;
        var risk = GenericScriptCatalogue.TryGetDefinition(scriptName, out var definition)
            ? definition.Risk.ToString()
            : "Unknown";
        var target = parameters.FirstOrDefault(pair => string.Equals(pair.Key, "SamAccountName", StringComparison.OrdinalIgnoreCase)).Value;

        return new ExecutionAuditRecord
        {
            TimestampUtc = DateTime.UtcNow,
            EventId = Guid.NewGuid(),
            Actor = string.IsNullOrWhiteSpace(context?.Actor) ? "unknown" : context.Actor,
            ScriptName = scriptName,
            ExecutionMode = mode,
            Source = context?.Source ?? "Unknown",
            Risk = risk,
            TargetSamAccountName = string.IsNullOrWhiteSpace(target) ? null : target,
            Outcome = "Started"
        };
    }

    private static ExecutionAuditRecord Clone(ExecutionAuditRecord record) => new()
    {
        TimestampUtc = record.TimestampUtc,
        EventId = record.EventId,
        Actor = record.Actor,
        ScriptName = record.ScriptName,
        ExecutionMode = record.ExecutionMode,
        Source = record.Source,
        Risk = record.Risk,
        TargetSamAccountName = record.TargetSamAccountName,
        Outcome = record.Outcome,
        DurationMs = record.DurationMs
    };

    private static bool TryReadDoneSuccess(string line, out bool success)
    {
        success = false;
        if (!line.StartsWith("data:", StringComparison.Ordinal))
            return false;

        try
        {
            using var document = JsonDocument.Parse(line["data:".Length..].Trim());
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) ||
                !string.Equals(type.GetString(), "done", StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("success", out var successProperty) ||
                successProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            success = successProperty.GetBoolean();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}