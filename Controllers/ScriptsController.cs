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
        var scripts = _powerShellService.GetAvailableScripts();
        return View(scripts);
    }

    public IActionResult Details(string name)
    {
        try
        {
            var script = _powerShellService.GetScriptDetails(name);
            return View(script);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Execute(string name, [FromBody] Dictionary<string, string> parameters)
    {
        try
        {
            var result = await _powerShellService.ExecuteScriptAsync(name, parameters ?? new Dictionary<string, string>());
            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new ScriptExecutionResult { Success = false, Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task Stream(string name, CancellationToken cancellationToken)
    {
        var parameters = Request.Query
            .Where(q => q.Key != "name")
            .ToDictionary(q => q.Key, q => q.Value.ToString());

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await _powerShellService.StreamScriptOutputAsync(name, parameters, async line =>
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
}
