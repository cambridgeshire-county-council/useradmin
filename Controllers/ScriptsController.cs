using Microsoft.AspNetCore.Mvc;
using PSScriptWebApp.Models;
using PSScriptWebApp.Services;

namespace PSScriptWebApp.Controllers;

public class ScriptsController : Controller
{
    private readonly IPowerShellService _powerShellService;

    public ScriptsController(IPowerShellService powerShellService)
    {
        _powerShellService = powerShellService;
    }

    public IActionResult Index()
    {
        var scripts = _powerShellService.GetAvailableScripts()
            .Where(script => GenericScriptCatalogue.TryGetCanonicalName(script.Name, out _))
            .ToList();
        return View(scripts);
    }

    public IActionResult Details(string name)
    {
        if (!GenericScriptCatalogue.TryGetCanonicalName(name, out var canonicalName))
        {
            return NotFound();
        }

        try
        {
            var script = _powerShellService.GetScriptDetails(canonicalName);
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
        if (!GenericScriptCatalogue.TryGetCanonicalName(name, out var canonicalName))
        {
            return NotFound();
        }

        try
        {
            var result = await _powerShellService.ExecuteScriptAsync(canonicalName, parameters ?? new Dictionary<string, string>());
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
        if (!GenericScriptCatalogue.TryGetCanonicalName(name, out var canonicalName))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await _powerShellService.StreamScriptOutputAsync(canonicalName, parameters ?? new Dictionary<string, string>(), async line =>
            {
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
}
