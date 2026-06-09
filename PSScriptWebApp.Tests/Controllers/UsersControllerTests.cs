using Microsoft.AspNetCore.Mvc;
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
        Assert.Equal(model.UserPrincipalName, stubService.LastParameters["UserPrincipalName"]);
        Assert.Equal(model.SamAccountName, stubService.LastParameters["SamAccountName"]);
        Assert.Equal(model.Description, stubService.LastParameters["Description"]);
        Assert.Equal(model.ExtensionAttribute2Value, stubService.LastParameters["ExtensionAttribute2Value"]);

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
            UserPrincipalName = "jane.smith@example.com",
            SamAccountName = "JS123",
            Description = "Test user",
            ExtensionAttribute2Value = "SchoolA"
        };
    }

    private sealed class StubPowerShellService : IPowerShellService
    {
        public ScriptExecutionResult ExecutionResult { get; set; } = new();
        public Dictionary<string, ScriptExecutionResult> ExecutionResults { get; set; } = new();
        public bool ExecuteCalled { get; private set; }
        public string? LastScriptName { get; private set; }
        public Dictionary<string, string>? LastParameters { get; private set; }

        public List<PowerShellScript> GetAvailableScripts() => new();

        public PowerShellScript GetScriptDetails(string scriptName)
        {
            return new PowerShellScript { Name = scriptName };
        }

        public Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, string> parameters)
        {
            ExecuteCalled = true;
            LastScriptName = scriptName;
            LastParameters = parameters;
            if (ExecutionResults.TryGetValue(scriptName, out var scriptResult))
            {
                return Task.FromResult(scriptResult);
            }

            return Task.FromResult(ExecutionResult);
        }
    }
}