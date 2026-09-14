using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PSScriptWebApp.Controllers;
using PSScriptWebApp.Models;
using PSScriptWebApp.Services;
using Xunit;

namespace PSScriptWebApp.Tests.Controllers;

public class ScriptsControllerTests
{
    [Fact]
    public void Index_ReturnsViewWithScripts()
    {
        var scripts = new List<PowerShellScript>
        {
            new()
            {
                Name = "Example",
                Path = "C:\\scripts\\Example.ps1"
            }
        };

        var controller = new ScriptsController(new StubPowerShellService
        {
            Scripts = scripts
        });

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<PowerShellScript>>(viewResult.Model);
        Assert.Same(scripts, model);
    }

    [Fact]
    public void Details_ReturnsNotFoundForMissingScript()
    {
        var controller = new ScriptsController(new StubPowerShellService
        {
            ThrowOnGetScriptDetails = true
        });

        var result = controller.Details("Missing");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Execute_ReturnsJsonResultFromService()
    {
        var expectedResult = new ScriptExecutionResult
        {
            Success = true,
            Output = "script output"
        };

        var controller = new ScriptsController(new StubPowerShellService
        {
            ExecutionResult = expectedResult
        });

        var result = await controller.Execute(
            "Example",
            new Dictionary<string, string>
            {
                ["Message"] = "Hello"
            });

        var jsonResult = Assert.IsType<JsonResult>(result);
        var model = Assert.IsType<ScriptExecutionResult>(jsonResult.Value);
        Assert.Same(expectedResult, model);
        Assert.True(model.Success);
        Assert.Equal("script output", model.Output);
    }

    [Fact]
    public void ExecutionEndpoints_RequirePostAndAntiforgeryValidation()
    {
        var executeMethod = typeof(ScriptsController).GetMethod(nameof(ScriptsController.Execute));
        var streamMethod = typeof(ScriptsController).GetMethods()
            .Single(method => method.Name == nameof(ScriptsController.Stream) && method.GetParameters().Length == 3);
        var getStreamMethod = typeof(ScriptsController).GetMethods()
            .Single(method => method.Name == nameof(ScriptsController.Stream) && method.GetParameters().Length == 1);

        Assert.NotNull(executeMethod);
        Assert.NotNull(streamMethod);
        Assert.NotNull(executeMethod.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(streamMethod.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(getStreamMethod.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(executeMethod.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(streamMethod.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
    }

    private sealed class StubPowerShellService : IPowerShellService
    {
        public List<PowerShellScript> Scripts { get; set; } = new();
        public ScriptExecutionResult ExecutionResult { get; set; } = new();
        public bool ThrowOnGetScriptDetails { get; set; }

        public List<PowerShellScript> GetAvailableScripts() => Scripts;

        public PowerShellScript GetScriptDetails(string scriptName)
        {
            if (ThrowOnGetScriptDetails)
            {
                throw new FileNotFoundException($"Script {scriptName} not found.");
            }

            return Scripts.FirstOrDefault(script => script.Name == scriptName)
                ?? new PowerShellScript { Name = scriptName };
        }

        public Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, string> parameters)
        {
            return Task.FromResult(ExecutionResult);
        }

        public Task StreamScriptOutputAsync(string scriptName, Dictionary<string, string> parameters, Func<string, Task> onLine, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}