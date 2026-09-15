namespace PSScriptWebApp.Services;

public static class GenericScriptCatalogue
{
    private static readonly HashSet<string> PermittedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NewUser",
        "Search",
        "GetUser",
        "SearchForDeletion",
        "MarkForDeletion",
        "UnmarkForDeletion",
        "GetMarkedForDeletion",
        "GetUserNotes",
        "DeleteUser"
    };

    public static bool TryGetCanonicalName(string? requestedName, out string canonicalName)
    {
        canonicalName = PermittedNames.FirstOrDefault(name =>
            string.Equals(name, requestedName, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        return canonicalName.Length > 0;
    }
}