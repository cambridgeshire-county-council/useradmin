using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PSScriptWebApp.Models;
using PSScriptWebApp.Services;

namespace PSScriptWebApp.Controllers;

public class UsersController : Controller
{
    private readonly IPowerShellService _powerShellService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            ["DisplayName"] = model.DisplayName,
            ["UserPrincipalName"] = model.UserPrincipalName,
            ["SamAccountName"] = model.SamAccountName,
            ["Office"] = model.Office ?? string.Empty,
            ["StreetAddress"] = model.StreetAddress ?? string.Empty,
            ["POBox"] = model.POBox ?? string.Empty,
            ["PostalCode"] = model.PostalCode ?? string.Empty,
            ["Country"] = model.Country,
            ["JobTitle"] = model.JobTitle ?? string.Empty,
            ["Department"] = model.Department ?? string.Empty,
            ["Company"] = model.Company,
            ["Manager"] = model.Manager ?? string.Empty
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

    [HttpGet]
    public async Task StreamNew(CancellationToken cancellationToken)
    {
        var parameters = Request.Query
            .ToDictionary(q => q.Key, q => q.Value.ToString());

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await _powerShellService.StreamScriptOutputAsync("NewUser", parameters, async sseEvent =>
            {
                var sanitized = SanitizeSseEvent(sseEvent);
                await Response.WriteAsync(sanitized, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var errorEvent = "data:" + JsonSerializer.Serialize(new { type = "done", success = false, error = ex.Message }) + "\n\n";
            await Response.WriteAsync(errorEvent, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet]
    public IActionResult Search()
    {
        return View(new UserSearchViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(UserSearchViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var executionResult = await _powerShellService.ExecuteScriptAsync(
            "Search",
            new Dictionary<string, string>
            {
                ["search"] = model.Search
            });

        if (!executionResult.Success)
        {
            model.Error = string.IsNullOrWhiteSpace(executionResult.Error)
                ? "Search script failed."
                : executionResult.Error;
            return View(model);
        }

        try
        {
            model.Results = ParseListOutput<UserSearchResultItem>(executionResult.Output);
        }
        catch (JsonException)
        {
            model.Error = "Search results could not be parsed.";
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string samAccountName)
    {
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            return BadRequest();
        }

        var executionResult = await _powerShellService.ExecuteScriptAsync(
            "GetUser",
            new Dictionary<string, string>
            {
                ["SamAccountName"] = samAccountName
            });

        if (!executionResult.Success)
        {
            return View(new UserDetailsViewModel
            {
                SamAccountName = samAccountName,
                Error = string.IsNullOrWhiteSpace(executionResult.Error)
                    ? "GetUser script failed."
                    : executionResult.Error
            });
        }

        try
        {
            var model = JsonSerializer.Deserialize<UserDetailsViewModel>(executionResult.Output, JsonOptions)
                ?? new UserDetailsViewModel();

            if (string.IsNullOrWhiteSpace(model.SamAccountName))
            {
                model.SamAccountName = samAccountName;
            }

            return View(model);
        }
        catch (JsonException)
        {
            return View(new UserDetailsViewModel
            {
                SamAccountName = samAccountName,
                Error = "User details could not be parsed."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> MarkForDeletion(string? search, string? status)
    {
        var model = new DeletionSearchViewModel
        {
            Search = search,
            StatusMessage = status
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var result = await _powerShellService.ExecuteScriptAsync(
                "SearchForDeletion",
                new Dictionary<string, string> { ["search"] = search });

            if (!result.Success)
            {
                model.Error = string.IsNullOrWhiteSpace(result.Error) ? "Search failed." : result.Error;
                return View(model);
            }

            try
            {
                model.Results = ParseListOutput<DeletionSearchResultItem>(result.Output);
            }
            catch (JsonException)
            {
                model.Error = "Search results could not be parsed.";
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkForDeletion(DeletionSearchViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Search))
        {
            return View(model);
        }

        var result = await _powerShellService.ExecuteScriptAsync(
            "SearchForDeletion",
            new Dictionary<string, string> { ["search"] = model.Search });

        if (!result.Success)
        {
            model.Error = string.IsNullOrWhiteSpace(result.Error) ? "Search failed." : result.Error;
            return View(model);
        }

        try
        {
            model.Results = ParseListOutput<DeletionSearchResultItem>(result.Output);
        }
        catch (JsonException)
        {
            model.Error = "Search results could not be parsed.";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUser(string samAccountName, string? returnSearch, string? notes)
    {
        var result = await _powerShellService.ExecuteScriptAsync(
            "MarkForDeletion",
            new Dictionary<string, string>
            {
                ["SamAccountName"] = samAccountName,
                ["Notes"] = notes ?? string.Empty
            });

        var status = result.Success
            ? $"'{samAccountName}' marked for deletion."
            : (string.IsNullOrWhiteSpace(result.Error) ? "Mark failed." : result.Error);

        return RedirectToAction(nameof(MarkForDeletion), new { search = returnSearch, status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnmarkUser(string samAccountName, string? returnSearch, string? notes)
    {
        var result = await _powerShellService.ExecuteScriptAsync(
            "UnmarkForDeletion",
            new Dictionary<string, string>
            {
                ["SamAccountName"] = samAccountName,
                ["Notes"] = notes ?? string.Empty
            });

        var status = result.Success
            ? $"'{samAccountName}' unmarked."
            : (string.IsNullOrWhiteSpace(result.Error) ? "Unmark failed." : result.Error);

        return RedirectToAction(nameof(MarkForDeletion), new { search = returnSearch, status });
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotes(string samAccountName)
    {
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            return BadRequest();
        }

        var result = await _powerShellService.ExecuteScriptAsync(
            "GetUserNotes",
            new Dictionary<string, string> { ["SamAccountName"] = samAccountName });

        if (!result.Success)
        {
            return StatusCode(500, new { error = string.IsNullOrWhiteSpace(result.Error) ? "Failed to retrieve notes." : result.Error });
        }

        try
        {
            using var document = JsonDocument.Parse(result.Output);
            var notes = document.RootElement.TryGetProperty("Notes", out var notesProp) && notesProp.ValueKind == JsonValueKind.String
                ? notesProp.GetString()
                : null;

            return new JsonResult(new { notes });
        }
        catch (JsonException)
        {
            return StatusCode(500, new { error = "Notes could not be parsed." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> MarkedForDeletion(string filter = "all", string? status = null)
    {
        var model = new MarkedForDeletionViewModel { Filter = filter, StatusMessage = status };

        var result = await _powerShellService.ExecuteScriptAsync(
            "GetMarkedForDeletion",
            new Dictionary<string, string>());

        if (!result.Success)
        {
            model.Error = string.IsNullOrWhiteSpace(result.Error) ? "Failed to retrieve marked users." : result.Error;
            return View(model);
        }

        try
        {
            var allUsers = ParseListOutput<MarkedUserItem>(result.Output);

            model.Users = filter switch
            {
                "week" => allUsers.Where(u => u.MarkedDate.HasValue && u.MarkedDate.Value <= DateTime.Today.AddDays(-7)).ToList(),
                "month" => allUsers.Where(u => u.MarkedDate.HasValue && u.MarkedDate.Value <= DateTime.Today.AddDays(-30)).ToList(),
                _ => allUsers
            };
        }
        catch (JsonException)
        {
            model.Error = "Marked users list could not be parsed.";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMarked(List<string> samAccountNames, string filter = "all")
    {
        int succeeded = 0;
        int failed = 0;

        foreach (var sam in samAccountNames)
        {
            var result = await _powerShellService.ExecuteScriptAsync(
                "DeleteUser",
                new Dictionary<string, string> { ["SamAccountName"] = sam });

            if (result.Success) succeeded++;
            else failed++;
        }

        var status = failed == 0
            ? $"{succeeded} user(s) deleted successfully."
            : $"{succeeded} deleted, {failed} failed.";

        return RedirectToAction(nameof(MarkedForDeletion), new { filter, status });
    }

    private static string? SanitizeOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return output;

        return Regex.Replace(
            output,
            @"(?im)^Generated Password:\s*.+$",
            "Generated Password: [hidden]");
    }

    private static string SanitizeSseEvent(string sseEvent)
    {
        if (!sseEvent.Contains("Generated Password", StringComparison.OrdinalIgnoreCase))
            return sseEvent;

        if (!sseEvent.StartsWith("data:"))
            return sseEvent;

        try
        {
            var jsonPart = sseEvent["data:".Length..].TrimEnd('\n');
            using var doc = JsonDocument.Parse(jsonPart);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString() ?? "line";
            if (root.TryGetProperty("text", out var textProp))
            {
                var sanitized = SanitizeOutput(textProp.GetString());
                return "data:" + JsonSerializer.Serialize(new { type, text = sanitized }) + "\n\n";
            }
        }
        catch { }

        return sseEvent;
    }

    private static List<T> ParseListOutput<T>(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new List<T>();
        }

        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<T>>(output, JsonOptions) ?? new List<T>();
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            var item = JsonSerializer.Deserialize<T>(output, JsonOptions);
            return item is null ? new List<T>() : new List<T> { item };
        }

        return new List<T>();
    }
}