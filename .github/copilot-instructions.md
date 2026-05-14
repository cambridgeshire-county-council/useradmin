# Copilot Instructions for PSScriptWebApp

## Project Purpose
PSScriptWebApp is an ASP.NET Core MVC web app (net9.0) that:
- Lists PowerShell scripts from the `scripts/` folder.
- Reads each script's `param(...)` block to build a dynamic HTML form.
- Executes a selected script through `powershell.exe` with user-provided parameter values.
- Returns script output/error back to the UI as JSON.

Primary user flow:
1. `ScriptsController.Index` lists available scripts.
2. `ScriptsController.Details` shows script parameters as form fields.
3. `ScriptsController.Execute` runs the script and returns `ScriptExecutionResult`.

## Key Files
- `Program.cs`: MVC and DI setup, routing, static assets.
- `Services/PowerShellService.cs`: script discovery, parameter parsing, script execution.
- `Controllers/ScriptsController.cs`: script endpoints and JSON execution response.
- `Models/PowerShellScript.cs`: `PowerShellScript`, `PowerShellParameter`, `ScriptExecutionResult`.
- `Views/Scripts/Index.cshtml`: script listing UI.
- `Views/Scripts/Details.cshtml`: dynamic input form and `fetch` POST execution.
- `scripts/*.ps1`: executable scripts surfaced by the app.

## Implementation Notes
- Scripts are discovered by file extension (`*.ps1`) under the content-root `scripts` directory.
- Parameter metadata is parsed with PowerShell AST (`System.Management.Automation.Language.Parser`).
- Current metadata extraction is string-based for:
  - `Mandatory=$true`
  - `HelpMessage="..."`
  - `DisplayName("...")` (custom pattern)
- Script execution uses `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script>` and appends each user parameter as `-Name Value`.

## Constraints and Guardrails
When editing this project:
- Preserve the `IPowerShellService` contract unless explicitly asked to break API.
- Keep script discovery rooted at `scripts/` unless requested otherwise.
- Avoid introducing breaking route changes in `ScriptsController`.
- Keep JSON shape stable for execute responses (`success`, `output`, `error` from `ScriptExecutionResult`).
- Prefer small targeted changes over broad refactors.

## Security and Reliability Priorities
Treat these as high priority in future changes:
- Validate and constrain `scriptName` inputs to known scripts.
- Validate parameter values before execution.
- Do not leak sensitive data in UI output or logs (scripts may emit credentials).
- Handle long-running scripts and cancellation/timeouts where possible.
- Keep error handling user-safe (no stack traces in browser responses).

## Coding Style
- Follow existing C# style in repository (`nullable` enabled, implicit usings enabled).
- Use async/await for I/O and process execution paths.
- Keep controllers thin; business logic belongs in services.
- Add comments only where logic is non-obvious.

## Build and Run
- Build: `dotnet build PSScriptWebApp.csproj`
- Run (watch): `dotnet watch run --project PSScriptWebApp.csproj`
- Publish: `dotnet publish PSScriptWebApp.csproj`

## What to Check After Changes
For changes touching script execution, parameters, or views:
- App starts without runtime exceptions.
- `Scripts` page lists all expected `.ps1` files.
- `Details` page renders the right number of inputs.
- Form submission returns and displays success/error output.
- Failed scripts return `success=false` and useful `error` text.

## Non-Goals (unless requested)
- Do not redesign the app UI framework.
- Do not move from MVC to API/SPA architecture.
- Do not switch execution engine away from PowerShell without explicit approval.
