using Microsoft.Extensions.Options;
using PSScriptWebApp.Models;
using PSScriptWebApp.Services;
using PSScriptWebApp.Tests.Support;
using Xunit;

namespace PSScriptWebApp.Tests.Services;

public class ExecutionAuditTests
{
    [Fact]
    public async Task JsonLinesWriterAppendsValidRecordsToDailyFile()
    {
        using var tempDirectory = new ScriptTestEnvironment();
        var auditDirectory = Path.Combine(tempDirectory.RootPath, "audit");
        var service = new JsonLinesExecutionAuditService(
            Options.Create(new AuditLoggingOptions { Directory = "audit" }),
            tempDirectory.CreateWebHostEnvironment());

        await service.WriteAsync(CreateRecord("Succeeded"));
        await service.WriteAsync(CreateRecord("Failed"));

        var path = Directory.GetFiles(auditDirectory, "useradmin-audit-*.jsonl").Single();
        var lines = await File.ReadAllLinesAsync(path);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.NotNull(System.Text.Json.JsonSerializer.Deserialize<ExecutionAuditRecord>(line)));
    }

    [Fact]
    public async Task AuditingServiceCapturesContextAndMinimisesParameters()
    {
        var audit = new RecordingAuditService();
        var context = new ExecutionAuditContextAccessor();
        var inner = new StubPowerShellService
        {
            ExecutionResult = new ScriptExecutionResult { Success = true, Output = "secret output" }
        };
        var service = new AuditingPowerShellService(inner, audit, context);
        using (context.Push(new ExecutionAuditContext("Dedicated", "CCC\\operator")))
        {
            await service.ExecuteScriptAsync("Search", new Dictionary<string, string>
            {
                ["search"] = "private search",
                ["SamAccountName"] = "ABC123"
            });
        }

        Assert.Equal(2, audit.Records.Count);
        var completed = Assert.Single(audit.Records, record => record.Outcome == "Succeeded");
        Assert.Equal("CCC\\operator", completed.Actor);
        Assert.Equal("Dedicated", completed.Source);
        Assert.Equal("Execute", completed.ExecutionMode);
        Assert.Equal("Search", completed.ScriptName);
        Assert.Equal("ReadOnly", completed.Risk);
        Assert.Equal("ABC123", completed.TargetSamAccountName);
        Assert.DoesNotContain("private search", audit.SerialisedRecords);
        Assert.DoesNotContain("secret output", audit.SerialisedRecords);
    }

    [Fact]
    public async Task AuditingServiceCapturesStreamModeAndFailure()
    {
        var audit = new RecordingAuditService();
        var context = new ExecutionAuditContextAccessor();
        var inner = new StubPowerShellService { StreamException = new InvalidOperationException("synthetic") };
        var service = new AuditingPowerShellService(inner, audit, context);

        using (context.Push(new ExecutionAuditContext("Generic", "CCC\\operator")))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.StreamScriptOutputAsync("Search", new Dictionary<string, string>(), _ => Task.CompletedTask, CancellationToken.None));
        }

        var completed = Assert.Single(audit.Records, record => record.Outcome == "Failed");
        Assert.Equal("Generic", completed.Source);
        Assert.Equal("Stream", completed.ExecutionMode);
    }

    [Fact]
    public async Task AuditingServiceDoesNotInvokePowerShellWhenStartAuditFails()
    {
        var inner = new StubPowerShellService();
        var service = new AuditingPowerShellService(inner, new ThrowingAuditService(), new ExecutionAuditContextAccessor());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteScriptAsync("Search", new Dictionary<string, string>()));

        Assert.False(inner.ExecuteCalled);
    }

    [Fact]
    public async Task AuditingServiceRecordsFailedExecution()
    {
        var audit = new RecordingAuditService();
        var inner = new StubPowerShellService
        {
            ExecutionResult = new ScriptExecutionResult { Success = false }
        };
        var service = new AuditingPowerShellService(inner, audit, new ExecutionAuditContextAccessor());

        await service.ExecuteScriptAsync("Search", new Dictionary<string, string>());

        Assert.Contains(audit.Records, record => record.Outcome == "Failed");
    }

    private static ExecutionAuditRecord CreateRecord(string outcome) => new()
    {
        TimestampUtc = DateTime.UtcNow,
        EventId = Guid.NewGuid(),
        Actor = "CCC\\operator",
        ScriptName = "Search",
        ExecutionMode = "Execute",
        Source = "Dedicated",
        Risk = "ReadOnly",
        Outcome = outcome
    };

    private sealed class RecordingAuditService : IExecutionAuditService
    {
        public List<ExecutionAuditRecord> Records { get; } = new();
        public string SerialisedRecords => string.Join("\n", Records.Select(record => System.Text.Json.JsonSerializer.Serialize(record)));

        public Task WriteAsync(ExecutionAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAuditService : IExecutionAuditService
    {
        public Task WriteAsync(ExecutionAuditRecord record, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("audit unavailable");
    }

    private sealed class StubPowerShellService : IPowerShellService
    {
        public ScriptExecutionResult ExecutionResult { get; set; } = new();
        public Exception? StreamException { get; set; }
        public bool ExecuteCalled { get; private set; }

        public List<PowerShellScript> GetAvailableScripts() => new();
        public PowerShellScript GetScriptDetails(string scriptName) => new() { Name = scriptName };
        public Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, string> parameters)
        {
            ExecuteCalled = true;
            return Task.FromResult(ExecutionResult);
        }
        public Task StreamScriptOutputAsync(string scriptName, Dictionary<string, string> parameters, Func<string, Task> onLine, CancellationToken cancellationToken) =>
            StreamException is null ? Task.CompletedTask : Task.FromException(StreamException);
    }
}
