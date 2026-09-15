namespace PSScriptWebApp.Models;

public class ExecutionAuditRecord
{
    public DateTime TimestampUtc { get; set; }
    public Guid EventId { get; set; }
    public string Actor { get; set; } = "unknown";
    public string ScriptName { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Risk { get; set; } = "Unknown";
    public string? TargetSamAccountName { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}