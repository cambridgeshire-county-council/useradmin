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
}
