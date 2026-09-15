using System.Management.Automation;

namespace PSScriptWebApp.Models;

public enum GenericScriptRisk
{
    ReadOnly,
    Mutation,
    Destructive
}

public class PowerShellScript
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<PowerShellParameter> Parameters { get; set; } = new();
    public GenericScriptRisk Risk { get; set; }
    public bool RequiresConfirmation { get; set; }
    public bool RequiresSensitiveOutputSanitisation { get; set; }
    public string? WarningText { get; set; }
}

public class PowerShellParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string? DisplayName { get; set; }
    public string? HelpMessage { get; set; }
}

public class ScriptExecutionResult
{
    public string Output { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
