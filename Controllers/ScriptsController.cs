using Microsoft.AspNetCore.Mvc;
using PSScriptWebApp.Models;
using PSScriptWebApp.Services;

namespace PSScriptWebApp.Controllers;

public class ScriptsController : Controller
{
    private readonly IPowerShellService _powerShellService;
    private readonly IExecutionAuditService? _auditService;
    private readonly IExecutionAuditContextAccessor? _auditContext;
    private const string ConfirmationHeader = "X-UserAdmin-Confirm";

    public ScriptsController(IPowerShellService powerShellService, IExecutionAuditService? auditService = null, IExecutionAuditContextAccessor? auditContext = null)
    {
        _powerShellService = powerShellService;
        _auditService = auditService;
        _auditContext = auditContext;
    }

    public IActionResult Index()
    {
        var scripts = _powerShellService.GetAvailableScripts()
            .Where(script => GenericScriptCatalogue.TryGetDefinition(script.Name, out _))
            .Select(ApplyCatalogueMetadata)
            .ToList();
        return View(scripts);
    }

    public IActionResult Details(string name)
    {
        if (!GenericScriptCatalogue.TryGetDefinition(name, out var definition))
        {
            return NotFound();
        }

        try
        {
            var script = ApplyCatalogueMetadata(_powerShellService.GetScriptDetails(definition.Name));
            return View(script);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Execute(string name, [FromBody] Dictionary<string, string> parameters)
    {
        if (!GenericScriptCatalogue.TryGetDefinition(name, out var definition))
        {
            await AuditRejectedAsync(name, "Execute", parameters);
            return NotFound();
        }

        if (!HasRequiredConfirmation(definition))
        {
            await AuditRejectedAsync(definition.Name, "Execute", parameters);
            return BadRequest("Explicit operator confirmation is required for this script.");
        }

        try
        {
            using var auditContext = PushGenericAuditContext();
            var result = await _powerShellService.ExecuteScriptAsync(definition.Name, parameters ?? new Dictionary<string, string>());
            if (definition.RequiresSensitiveOutputSanitisation)
            {
                result.Output = ScriptOutputSanitizer.SanitizeOutput(result.Output) ?? string.Empty;
                result.Error = ScriptOutputSanitizer.SanitizeOutput(result.Error);
            }
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new ScriptExecutionResult { Success = false, Error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task Stream(string name, [FromBody] Dictionary<string, string>? parameters, CancellationToken cancellationToken)
    {
        if (!GenericScriptCatalogue.TryGetDefinition(name, out var definition))
        {
            await AuditRejectedAsync(name, "Stream", parameters);
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!HasRequiredConfirmation(definition))
        {
            await AuditRejectedAsync(definition.Name, "Stream", parameters);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            using var auditContext = PushGenericAuditContext();
            await _powerShellService.StreamScriptOutputAsync(definition.Name, parameters ?? new Dictionary<string, string>(), async line =>
            {
                if (definition.RequiresSensitiveOutputSanitisation)
                {
                    line = ScriptOutputSanitizer.SanitizeSseEvent(line);
                }
                await Response.WriteAsync(line, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var errorEvent = "data:" + System.Text.Json.JsonSerializer.Serialize(new { type = "done", success = false, error = ex.Message }) + "\n\n";
            await Response.WriteAsync(errorEvent, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet]
    public IActionResult Stream(string name)
    {
        return StatusCode(StatusCodes.Status405MethodNotAllowed);
    }

    private bool HasRequiredConfirmation(GenericScriptDefinition definition)
    {
        return !definition.RequiresConfirmation ||
            string.Equals(Request.Headers[ConfirmationHeader].ToString(), definition.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static PowerShellScript ApplyCatalogueMetadata(PowerShellScript script)
    {
        if (GenericScriptCatalogue.TryGetDefinition(script.Name, out var definition))
        {
            script.Risk = definition.Risk;
            script.RequiresConfirmation = definition.RequiresConfirmation;
            script.RequiresSensitiveOutputSanitisation = definition.RequiresSensitiveOutputSanitisation;
            script.WarningText = definition.WarningText;
        }

        return script;
    }

    private IDisposable? PushGenericAuditContext()
    {
        return _auditContext?.Push(new ExecutionAuditContext(
            "Generic",
            User.Identity?.Name ?? "unknown"));
    }

    private async Task AuditRejectedAsync(string scriptName, string mode, Dictionary<string, string>? parameters)
    {
        if (_auditService is null)
            return;

        var target = parameters?.FirstOrDefault(pair =>
            string.Equals(pair.Key, "SamAccountName", StringComparison.OrdinalIgnoreCase)).Value;
        await _auditService.WriteAsync(new Models.ExecutionAuditRecord
        {
            TimestampUtc = DateTime.UtcNow,
            EventId = Guid.NewGuid(),
            Actor = User.Identity?.Name ?? "unknown",
            ScriptName = scriptName,
            ExecutionMode = mode,
            Source = "Generic",
            Risk = GenericScriptCatalogue.TryGetDefinition(scriptName, out var definition)
                ? definition.Risk.ToString()
                : "Unknown",
            TargetSamAccountName = string.IsNullOrWhiteSpace(target) ? null : target,
            Outcome = "Rejected",
            DurationMs = 0
        });
    }
}
