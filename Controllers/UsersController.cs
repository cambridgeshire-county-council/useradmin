using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PSScriptWebApp.Models;
using PSScriptWebApp.Services;

namespace PSScriptWebApp.Controllers;

public class UsersController : Controller
{
    private readonly IPowerShellService _powerShellService;

    public UsersController(IPowerShellService powerShellService)
    {
        _powerShellService = powerShellService;
    }

    [HttpGet]
    public IActionResult New()
    {
        return View(new NewUserFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(NewUserFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var parameters = new Dictionary<string, string>
        {
            ["FirstName"] = model.FirstName,
            ["LastName"] = model.LastName,
            ["UserPrincipalName"] = model.UserPrincipalName,
            ["SamAccountName"] = model.SamAccountName,
            ["Description"] = model.Description,
            ["ExtensionAttribute2Value"] = model.ExtensionAttribute2Value
        };

        var result = await _powerShellService.ExecuteScriptAsync("NewUser", parameters);

        model.ExecutionSucceeded = result.Success;
        model.ExecutionOutput = SanitizeOutput(result.Output);
        model.ExecutionError = result.Error;

        if (!result.Success && string.IsNullOrWhiteSpace(model.ExecutionError))
        {
            model.ExecutionError = "The NewUser script failed. Check script output for details.";
        }

        return View(model);
    }

    private static string? SanitizeOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        return Regex.Replace(
            output,
            @"(?im)^Generated Password:\s*.+$",
            "Generated Password: [hidden]");
    }
}