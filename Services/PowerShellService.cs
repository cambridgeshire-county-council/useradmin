using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Language;
using System.Management.Automation.Host;
using PSScriptWebApp.Models;

namespace PSScriptWebApp.Services;

public interface IPowerShellService
{
    List<PowerShellScript> GetAvailableScripts();
    PowerShellScript GetScriptDetails(string scriptName);
    Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, string> parameters);
}

public class PowerShellService : IPowerShellService
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _scriptsPath;

    public PowerShellService(IWebHostEnvironment environment)
    {
        _environment = environment;
        _scriptsPath = Path.Combine(_environment.ContentRootPath, "scripts");
    }

    public List<PowerShellScript> GetAvailableScripts()
    {
        var scripts = new List<PowerShellScript>();
        if (!Directory.Exists(_scriptsPath))
            return scripts;

        foreach (var file in Directory.GetFiles(_scriptsPath, "*.ps1"))
        {
            scripts.Add(new PowerShellScript
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Path = file,
                Parameters = GetScriptParameters(file)
            });
        }

        return scripts;
    }

    public PowerShellScript GetScriptDetails(string scriptName)
    {
        var scriptPath = Path.Combine(_scriptsPath, $"{scriptName}.ps1");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Script {scriptName} not found.");

        return new PowerShellScript
        {
            Name = scriptName,
            Path = scriptPath,
            Parameters = GetScriptParameters(scriptPath)
        };
    }

    private List<PowerShellParameter> GetScriptParameters(string scriptPath)
    {
        var parameters = new List<PowerShellParameter>();
        var scriptContent = File.ReadAllText(scriptPath);
        var ast = Parser.ParseInput(scriptContent, out _, out _);
        
        if (ast.ParamBlock?.Parameters == null)
            return parameters;

        foreach (var param in ast.ParamBlock.Parameters)
        {
            var parameter = new PowerShellParameter
            {
                Name = param.Name.VariablePath.UserPath,
                Type = param.StaticType.Name
            };

            // Extract attributes from comments and parameter text
            var parameterText = param.Extent.Text;
            
            // Check for Mandatory
            parameter.IsMandatory = parameterText.Contains("Mandatory=$true");

            // Extract DisplayName
            if (parameterText.Contains("[DisplayName("))
            {
                var start = parameterText.IndexOf("[DisplayName(\"") + "[DisplayName(\"".Length;
                var end = parameterText.IndexOf("\")]", start);
                if (end > start)
                {
                    parameter.DisplayName = parameterText.Substring(start, end - start);
                }
            }

            // Extract HelpMessage
            if (parameterText.Contains("HelpMessage="))
            {
                var start = parameterText.IndexOf("HelpMessage=\"") + "HelpMessage=\"".Length;
                var end = parameterText.IndexOf("\")", start);
                if (end > start)
                {
                    parameter.HelpMessage = parameterText.Substring(start, end - start);
                }
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    public async Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, string> parameters)
    {
        var scriptPath = Path.Combine(_scriptsPath, $"{scriptName}.ps1");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Script {scriptName} not found.");

        var result = new ScriptExecutionResult();

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);

            foreach (var param in parameters)
            {
                startInfo.ArgumentList.Add($"-{param.Key}");
                startInfo.ArgumentList.Add(param.Value ?? string.Empty);
            }

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start powershell.exe process.");

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            result.Output = string.IsNullOrWhiteSpace(stdErr)
                ? stdOut
                : string.Join(Environment.NewLine, stdOut, stdErr).Trim();
            result.Success = process.ExitCode == 0;

            if (!result.Success && string.IsNullOrWhiteSpace(result.Error))
                result.Error = string.IsNullOrWhiteSpace(stdErr) ? "PowerShell exited with a non-zero exit code." : stdErr.Trim();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }
}
