using Microsoft.AspNetCore.Hosting;

namespace PSScriptWebApp.Tests.Support;

internal sealed class ScriptTestEnvironment : IDisposable
{
    public ScriptTestEnvironment()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "PSScriptWebApp.Tests", Guid.NewGuid().ToString("N"));
        ScriptsPath = Path.Combine(RootPath, "scripts");
        Directory.CreateDirectory(ScriptsPath);
    }

    public string RootPath { get; }
    public string ScriptsPath { get; }

    public IWebHostEnvironment CreateWebHostEnvironment() => new TestWebHostEnvironment(RootPath);

    public string WriteScript(string name, string content)
    {
        var scriptPath = Path.Combine(ScriptsPath, $"{name}.ps1");
        File.WriteAllText(scriptPath, content);
        return scriptPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}