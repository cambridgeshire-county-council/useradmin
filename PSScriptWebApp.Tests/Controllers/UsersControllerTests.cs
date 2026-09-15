using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Text;
using PSScriptWebApp.Controllers;
using PSScriptWebApp.Models;
using PSScriptWebApp.Services;
using Xunit;

namespace PSScriptWebApp.Tests.Controllers;

public class UsersControllerTests
{
    [Fact]
    public void New_Get_ReturnsViewWithModel()
    {
        var controller = new UsersController(new StubPowerShellService());

        var result = controller.New();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<NewUserFormModel>(viewResult.Model);
    }

    [Fact]
    public async Task New_Post_InvalidModel_ReturnsViewWithoutExecutingScript()
    {
        var stubService = new StubPowerShellService();
        var controller = new UsersController(stubService);
        controller.ModelState.AddModelError("FirstName", "Required");

        var model = CreateValidModel();
        var result = await controller.New(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(model, viewResult.Model);
        Assert.False(stubService.ExecuteCalled);
    }

    [Fact]
    public async Task New_Post_ValidModel_ExecutesNewUserScriptWithMappedParameters()
    {
        var stubService = new StubPowerShellService
        {
            ExecutionResult = new ScriptExecutionResult
            {
                Success = true,
                Output = "Account created successfully.\nGenerated Password: SuperSecret123!"
            }
        };

        var controller = new UsersController(stubService);
        var model = CreateValidModel();

        var result = await controller.New(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        var returnedModel = Assert.IsType<NewUserFormModel>(viewResult.Model);

        Assert.True(stubService.ExecuteCalled);
        Assert.Equal("NewUser", stubService.LastScriptName);
        Assert.NotNull(stubService.LastParameters);
        Assert.Equal(model.FirstName, stubService.LastParameters!["FirstName"]);
        Assert.Equal(model.LastName, stubService.LastParameters["LastName"]);
        Assert.Equal(model.DisplayName, stubService.LastParameters["DisplayName"]);
        Assert.Equal(model.UserPrincipalName, stubService.LastParameters["UserPrincipalName"]);
        Assert.Equal(model.SamAccountName, stubService.LastParameters["SamAccountName"]);
        Assert.Equal(model.Company, stubService.LastParameters["Company"]);
        Assert.Equal(model.Country, stubService.LastParameters["Country"]);

        Assert.True(returnedModel.ExecutionSucceeded);
        Assert.Contains("Generated Password: [hidden]", returnedModel.ExecutionOutput);
        Assert.DoesNotContain("SuperSecret123!", returnedModel.ExecutionOutput);
    }

    [Fact]
    public async Task New_Post_FailedExecution_SetsFallbackErrorMessage()
    {
        var stubService = new StubPowerShellService
        {
            ExecutionResult = new ScriptExecutionResult
            {
                Success = false,
                Output = "Execution failed",
                Error = string.Empty
            }
        };

        var controller = new UsersController(stubService);
        var model = CreateValidModel();
        var result = await controller.New(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        var returnedModel = Assert.IsType<NewUserFormModel>(viewResult.Model);

        Assert.False(returnedModel.ExecutionSucceeded);
        Assert.Equal("The NewUser script failed. Check script output for details.", returnedModel.ExecutionError);
    }

    [Fact]
    public void StreamNew_RequiresPostAndAntiforgeryValidation()
    {
        var streamMethod = typeof(UsersController).GetMethod(nameof(UsersController.StreamNew));

        Assert.NotNull(streamMethod);
        Assert.NotNull(streamMethod.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).SingleOrDefault());
        Assert.Empty(streamMethod.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true));
        Assert.NotNull(streamMethod.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(streamMethod.GetParameters()
            .Single(parameter => parameter.Name == "model")
            .GetCustomAttributes(typeof(FromBodyAttribute), inherit: true)
            .SingleOrDefault());
    }

    [Fact]
    public async Task StreamNew_ValidModelMapsParametersAndSanitisesGeneratedPassword()
    {
        var stubService = new StubPowerShellService
        {
            StreamOutput = "data:{\"type\":\"line\",\"text\":\"Generated Password: Secret123!\"}\n\n"
        };
        var controller = CreateController(stubService);
        var model = CreateValidModel();

        await controller.StreamNew(model, CancellationToken.None);

        Assert.True(stubService.StreamCalled);
        Assert.Equal("NewUser", stubService.LastStreamScriptName);
        Assert.NotNull(stubService.LastStreamParameters);
        Assert.Equal(14, stubService.LastStreamParameters!.Count);
        Assert.Equal(model.FirstName, stubService.LastStreamParameters["FirstName"]);
        Assert.Equal(model.SamAccountName, stubService.LastStreamParameters["SamAccountName"]);
        Assert.Equal(model.Manager ?? string.Empty, stubService.LastStreamParameters["Manager"]);

        var response = await ReadResponseAsync(controller);
        Assert.Contains("Generated Password: [hidden]", response);
        Assert.DoesNotContain("Secret123!", response);
    }

    [Fact]
    public async Task StreamNew_InvalidModelDoesNotInvokePowerShellService()
    {
        var stubService = new StubPowerShellService();
        var controller = CreateController(stubService);
        controller.ModelState.AddModelError(nameof(NewUserFormModel.FirstName), "Required");

        await controller.StreamNew(CreateValidModel(), CancellationToken.None);

        Assert.False(stubService.StreamCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, controller.Response.StatusCode);
    }

    [Fact]
    public async Task DeleteMarked_EmptySubmissionDoesNotInvokeDeletion()
    {
        var stubService = new StubPowerShellService();
        var controller = new UsersController(stubService);

        var result = await controller.DeleteMarked(new List<string>(), "all");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.DoesNotContain("DeleteUser", stubService.ExecutedScriptNames);
    }

    [Fact]
    public async Task DeleteMarked_DeduplicatesCaseInsensitiveMarkedAccounts()
    {
        var stubService = CreateDeletionStub("[{\"samAccountName\":\"ABC123\",\"extensionAttribute3\":\"2026-01-01\"}]");
        var controller = new UsersController(stubService);

        await controller.DeleteMarked(new List<string> { "abc123", "ABC123", "abc123" });

        Assert.Equal(1, stubService.ExecutedScriptNames.Count(name => name == "DeleteUser"));
    }

    [Fact]
    public async Task DeleteMarked_RejectsUnmarkedAccountWithoutDeletingAnyAccount()
    {
        var stubService = CreateDeletionStub("[{\"samAccountName\":\"ABC123\",\"extensionAttribute3\":\"2026-01-01\"}]");
        var controller = new UsersController(stubService);

        var result = await controller.DeleteMarked(new List<string> { "ABC123", "NOTMARKED" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("not currently marked", redirect.RouteValues!["status"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeleteUser", stubService.ExecutedScriptNames);
    }

    [Fact]
    public async Task DeleteMarked_MalformedMarkedUserOutputFailsClosed()
    {
        var stubService = CreateDeletionStub("not-json");
        var controller = new UsersController(stubService);

        var result = await controller.DeleteMarked(new List<string> { "ABC123" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("not currently marked", redirect.RouteValues!["status"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeleteUser", stubService.ExecutedScriptNames);
    }

    [Fact]
    public async Task DeleteMarked_ValidatedMarkedAccountsPreserveReporting()
    {
        var stubService = CreateDeletionStub("[{\"samAccountName\":\"ABC123\",\"extensionAttribute3\":\"2026-01-01\"},{\"samAccountName\":\"XYZ789\",\"extensionAttribute3\":\"2026-01-02\"}]");
        var controller = new UsersController(stubService);

        var result = await controller.DeleteMarked(new List<string> { "ABC123", "xyz789" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("2 user(s) deleted successfully.", redirect.RouteValues!["status"]?.ToString());
        Assert.Equal(2, stubService.ExecutedScriptNames.Count(name => name == "DeleteUser"));
    }

    [Fact]
    public async Task Search_Post_ValidModel_ParsesResults()
    {
        var stubService = new StubPowerShellService
        {
            ExecutionResults = new Dictionary<string, ScriptExecutionResult>
            {
                ["Search"] = new ScriptExecutionResult
                {
                    Success = true,
                    Output = "[{\"name\":\"Jane Smith\",\"samAccountName\":\"JS123\",\"userPrincipalName\":\"jane.smith@example.com\",\"enabled\":true}]"
                }
            }
        };

        var controller = new UsersController(stubService);
        var model = new UserSearchViewModel { Search = "Jane" };

        var result = await controller.Search(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        var returnedModel = Assert.IsType<UserSearchViewModel>(viewResult.Model);

        Assert.Single(returnedModel.Results);
        Assert.Equal("JS123", returnedModel.Results[0].SamAccountName);
        Assert.Equal("Search", stubService.LastScriptName);
        Assert.Equal("Jane", stubService.LastParameters!["search"]);
    }

    [Fact]
    public async Task Details_Get_ReturnsParsedUserDetails()
    {
        var stubService = new StubPowerShellService
        {
            ExecutionResults = new Dictionary<string, ScriptExecutionResult>
            {
                ["GetUser"] = new ScriptExecutionResult
                {
                    Success = true,
                    Output = "{\"samAccountName\":\"JS123\",\"displayName\":\"Jane Smith\",\"userPrincipalName\":\"jane.smith@example.com\",\"enabled\":true}"
                }
            }
        };

        var controller = new UsersController(stubService);

        var result = await controller.Details("JS123");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UserDetailsViewModel>(viewResult.Model);

        Assert.Equal("JS123", model.SamAccountName);
        Assert.Equal("Jane Smith", model.DisplayName);
        Assert.True(model.Enabled);
        Assert.Equal("GetUser", stubService.LastScriptName);
        Assert.Equal("JS123", stubService.LastParameters!["SamAccountName"]);
    }

    private static NewUserFormModel CreateValidModel()
    {
        return new NewUserFormModel
        {
            FirstName = "Jane",
            LastName = "Smith",
            DisplayName = "Jane Smith",
            UserPrincipalName = "jane.smith@cambridgeshire.gov.uk",
            SamAccountName = "JS123"
        };
    }

    private static UsersController CreateController(StubPowerShellService stubService)
    {
        var controller = new UsersController(stubService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Response.Body = new MemoryStream();
        return controller;
    }

    private static StubPowerShellService CreateDeletionStub(string markedUsersOutput)
    {
        return new StubPowerShellService
        {
            ExecutionResults = new Dictionary<string, ScriptExecutionResult>
            {
                ["GetMarkedForDeletion"] = new ScriptExecutionResult
                {
                    Success = true,
                    Output = markedUsersOutput
                },
                ["DeleteUser"] = new ScriptExecutionResult
                {
                    Success = true
                }
            }
        };
    }

    private static async Task<string> ReadResponseAsync(UsersController controller)
    {
        controller.Response.Body.Position = 0;
        using var reader = new StreamReader(controller.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class StubPowerShellService : IPowerShellService
    {
        public ScriptExecutionResult ExecutionResult { get; set; } = new();
        public Dictionary<string, ScriptExecutionResult> ExecutionResults { get; set; } = new();
        public bool ExecuteCalled { get; private set; }
        public string? LastScriptName { get; private set; }
        public Dictionary<string, string>? LastParameters { get; private set; }
        public List<string> ExecutedScriptNames { get; } = new();
        public bool StreamCalled { get; private set; }
        public string? LastStreamScriptName { get; private set; }
        public Dictionary<string, string>? LastStreamParameters { get; private set; }
        public string? StreamOutput { get; set; }

        public List<PowerShellScript> GetAvailableScripts() => new();

        public PowerShellScript GetScriptDetails(string scriptName)
        {
            return new PowerShellScript { Name = scriptName };
        }

        public Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, string> parameters)
        {
            ExecuteCalled = true;
            ExecutedScriptNames.Add(scriptName);
            LastScriptName = scriptName;
            LastParameters = parameters;
            if (ExecutionResults.TryGetValue(scriptName, out var scriptResult))
            {
                return Task.FromResult(scriptResult);
            }

            return Task.FromResult(ExecutionResult);
        }

        public Task StreamScriptOutputAsync(string scriptName, Dictionary<string, string> parameters, Func<string, Task> onLine, CancellationToken cancellationToken)
        {
            StreamCalled = true;
            LastStreamScriptName = scriptName;
            LastStreamParameters = parameters;
            return StreamOutput is null ? Task.CompletedTask : onLine(StreamOutput);
        }
    }
}