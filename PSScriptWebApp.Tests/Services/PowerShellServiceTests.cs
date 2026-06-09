using PSScriptWebApp.Services;
using PSScriptWebApp.Tests.Support;
using Xunit;

namespace PSScriptWebApp.Tests.Services;

public class PowerShellServiceTests
{
    [Fact]
    public void GetAvailableScripts_ReturnsScriptsAndParsesParameters()
    {
        using var testEnvironment = new ScriptTestEnvironment();
        var expectedPath = testEnvironment.WriteScript(
            "Parsed",
            """
            param(
                [Parameter(Mandatory=$true)]
                [string]$Name,

                [DisplayName("Run Count")]
                [Parameter(HelpMessage="How many times to run")]
                [int]$Count
            )

            Write-Output "done"
            """);

        var service = new PowerShellService(testEnvironment.CreateWebHostEnvironment());

        var scripts = service.GetAvailableScripts();

        var script = Assert.Single(scripts);
        Assert.Equal("Parsed", script.Name);
        Assert.Equal(expectedPath, script.Path);
        Assert.Equal(2, script.Parameters.Count);

        var nameParameter = script.Parameters[0];
        Assert.Equal("Name", nameParameter.Name);
        Assert.Equal("String", nameParameter.Type);
        Assert.True(nameParameter.IsMandatory);

        var countParameter = script.Parameters[1];
        Assert.Equal("Count", countParameter.Name);
        Assert.Equal("Int32", countParameter.Type);
        Assert.False(countParameter.IsMandatory);
        Assert.Equal("Run Count", countParameter.DisplayName);
        Assert.Equal("How many times to run", countParameter.HelpMessage);
    }

    [Fact]
    public void GetScriptDetails_ThrowsForMissingScript()
    {
        using var testEnvironment = new ScriptTestEnvironment();
        var service = new PowerShellService(testEnvironment.CreateWebHostEnvironment());

        var exception = Assert.Throws<FileNotFoundException>(() => service.GetScriptDetails("Missing"));

        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public async Task ExecuteScriptAsync_ReturnsOutputForExistingScript()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var testEnvironment = new ScriptTestEnvironment();
        testEnvironment.WriteScript(
            "Echo",
            """
            param(
                [string]$Message
            )

            Write-Output "Message: $Message"
            """);

        var service = new PowerShellService(testEnvironment.CreateWebHostEnvironment());

        var result = await service.ExecuteScriptAsync(
            "Echo",
            new Dictionary<string, string>
            {
                ["Message"] = "Hello"
            });

        Assert.True(result.Success);
        Assert.Contains("Message: Hello", result.Output);
        Assert.True(string.IsNullOrWhiteSpace(result.Error));
    }
}