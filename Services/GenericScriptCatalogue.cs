namespace PSScriptWebApp.Services;

using PSScriptWebApp.Models;

public sealed record GenericScriptDefinition(
    string Name,
    GenericScriptRisk Risk,
    bool RequiresConfirmation,
    bool RequiresSensitiveOutputSanitisation,
    string? WarningText);

public static class GenericScriptCatalogue
{
    private static readonly IReadOnlyList<GenericScriptDefinition> Definitions = new GenericScriptDefinition[]
    {
        new("NewUser", GenericScriptRisk.Mutation, true, true, "This script changes live directory state."),
        new("Search", GenericScriptRisk.ReadOnly, false, false, null),
        new("GetUser", GenericScriptRisk.ReadOnly, false, false, null),
        new("SearchForDeletion", GenericScriptRisk.ReadOnly, false, false, null),
        new("MarkForDeletion", GenericScriptRisk.Mutation, true, false, "This script changes live directory state."),
        new("UnmarkForDeletion", GenericScriptRisk.Mutation, true, false, "This script changes live directory state."),
        new("GetMarkedForDeletion", GenericScriptRisk.ReadOnly, false, false, null),
        new("GetUserNotes", GenericScriptRisk.ReadOnly, false, false, null),
        new("DeleteUser", GenericScriptRisk.Destructive, true, false, "This script performs a destructive operation that may be irreversible.")
    };

    public static IReadOnlyList<GenericScriptDefinition> GetDefinitions() => Definitions;

    public static bool TryGetDefinition(string? requestedName, out GenericScriptDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.Name, requestedName, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static bool TryGetCanonicalName(string? requestedName, out string canonicalName)
    {
        if (TryGetDefinition(requestedName, out var definition))
        {
            canonicalName = definition.Name;
            return true;
        }

        canonicalName = string.Empty;
        return false;
    }
}