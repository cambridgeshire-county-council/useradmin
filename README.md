# UserAdmin Toolbox

An ASP.NET Core MVC (net9.0) web app that lets users run PowerShell scripts from a browser instead of a terminal.

It works by:
- Discovering `.ps1` files in the `scripts/` folder.
- Parsing each script's `param(...)` block (via PowerShell AST) to build a dynamic HTML form.
- Executing the selected script through `powershell.exe` with the submitted parameter values.
- Returning the script's output/error back to the UI as JSON.

There are some hard-coded MVC routes that allow better customisation of the interface, but behind the scenes they are using the PowerShell scripts in the Scripts folder.


## Deployment

The site is deployed as an IIS website on CCCS923. It is accessible at http://uatoolbox.ccc.cambridgeshire.gov.uk (https certificate needs to be added to the server)

### Pipeline

1. On push to `main`, the `Build Deploy Zip` GitHub Actions workflow (`.github/workflows/build-deploy-zip.yml`) restores, tests, builds, and `dotnet publish`s the app, then uploads a versioned zip + SHA256 checksum as the `webapp-zip` artifact.
2. `Deploy.ps1` (run on the target IIS server) downloads the latest successful build's artifact, verifies its checksum, backs up the current site, stops the `ScriptRunner` app pool, replaces the site contents (preserving the existing `appsettings.json`), and restarts the app pool — rolling back automatically if any step fails.
3. For local/manual builds, `PublishAndZip.ps1` runs `dotnet publish` and packages the output into a zip without going through GitHub Actions.

## Example feature deployment

Changes are best made by assigning work to the GitHub Copilot coding agent rather than editing code by hand.

1. Go to https://github.com/cambridgeshire-county-council/useradmin/agents and start a new task on the `main` branch.
2. Describe the change as a clear, well-formatted list of instructions rather than a vague request. For example:

   ```
   Make the following UI changes to PSScriptWebApp:

   1. Change the visible app name in the GUI from "PSScriptRunner" to "UserAdmin Toolbox".
   2. Remove "Home" from the top navigation bar.
   3. Rename "Scripts" in the top navigation bar to "Powershell Script list" and move it to 
      the last position, after all other nav items.
   4. On the Home view, add links to "New" and "Search" (and any other relevant script
      actions) so users can jump straight to them without going via the Scripts list.

   Keep changes scoped to views/layout/navigation only — do not alter script discovery,
   execution, or the ScriptExecutionResult JSON contract.
   ```

   or

   ```
   Add script execution logging to PSScriptWebApp.

   1. Whenever a script is run via ScriptsController.Execute, log an entry containing:
      timestamp, the Windows username from User.Identity.Name, the script name, and the
      parameter names/values submitted.
   2. Do not log parameter values for any parameter that looks like a credential or secret
      (e.g. named "Password", "Token", "Secret") — redact these as "*****" in the log.
   3. Write logs as plain text to a log/ folder under the content root, one file per day
      (e.g. log/2026-08-20.txt), so files don't grow unbounded.
   4. Add a "Logs" page (nav item, after "Powershell Script list") that lists available log
      dates and displays the selected day's entries in a readable table, newest first.
   5. Restrict the Logs page to the same authorization policy already used elsewhere in the
      app — do not expose it anonymously.

   Keep the existing ScriptExecutionResult JSON contract and execution flow unchanged; this
   should be additive logging only.
   ```

   Tip: have an agent suggest improvements to your prompt before submitting it

3. The agent opens a pull request against `main` (sometimes a pull request isn't created, you can still browse to the branch though). Review the diff and confirm the `Build Deploy Zip` GitHub Actions check passes before merging.
4. Once merged and the workflow run is green, deploy by running `Deploy.ps1` on CCCS923 to pull down and install the latest build (see [Pipeline](#pipeline) above).
