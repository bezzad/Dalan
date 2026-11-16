namespace Dalan.Architecture.Tests;

/// <summary>The ARC rules from openspec/specs/architecture as one pure check. Returns one line per broken rule.</summary>
public static class ArchitectureRules
{
    public static IReadOnlyList<string> Check(IReadOnlyList<Project> projects)
    {
        var kinds = projects.ToDictionary(p => p.Name, p => p.Kind);
        var errors = new List<string>();

        foreach (var p in projects)
        foreach (var r in p.References)
        {
            var target = kinds.GetValueOrDefault(r, ProjectKind.Service);

            if (p.Kind == ProjectKind.Service && target == ProjectKind.Service)
                errors.Add($"ARC-01: {p.Name} references service implementation {r}");

            if (p.Kind == ProjectKind.Contracts && target != ProjectKind.Contracts)
                errors.Add($"ARC-02: contracts {p.Name} references {r}");

            if (IsRelay(p.Name) && r.Contains("Telegram", StringComparison.Ordinal))
                errors.Add($"ARC-03: {p.Name} references {r}");

            if (p.Kind == ProjectKind.Plugin && target != ProjectKind.Contracts)
                errors.Add($"ARC-04: plugin {p.Name} references {r}");

            if (p.Kind != ProjectKind.Host && target is ProjectKind.Plugin or ProjectKind.Host)
                errors.Add($"ARC-05: only the host may reference {r}, but {p.Name} does");
        }

        return errors;
    }

    public static bool IsRelay(string projectName) =>
        projectName is "Dalan.Relay" or "Dalan.Relay.Contracts";
}
