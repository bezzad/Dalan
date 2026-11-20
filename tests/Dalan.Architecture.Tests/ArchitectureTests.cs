using System.Reflection;

namespace Dalan.Architecture.Tests;

public class ArchitectureTests
{
    private static Project P(string name, ProjectKind kind, params string[] refs) => new(name, kind, refs);

    private static readonly Project[] Good =
    [
        P("Dalan.Host", ProjectKind.Host, "Dalan.Relay", "Dalan.Identity", "Dalan.Telegram"),
        P("Dalan.Relay", ProjectKind.Service, "Dalan.Relay.Contracts", "Dalan.Identity.Contracts"),
        P("Dalan.Relay.Contracts", ProjectKind.Contracts),
        P("Dalan.Identity", ProjectKind.Service, "Dalan.Identity.Contracts"),
        P("Dalan.Identity.Contracts", ProjectKind.Contracts),
        P("Dalan.Telegram", ProjectKind.Plugin, "Dalan.Relay.Contracts"),
    ];

    private static IReadOnlyList<string> CheckWith(Project changed) =>
        ArchitectureRules.Check(Good.Where(p => p.Name != changed.Name).Append(changed).ToList());

    [Fact]
    public void Good_graph_has_no_errors() => Assert.Empty(ArchitectureRules.Check(Good));

    [Fact]
    [Trait("Rule", "ARC-01")]
    public void ARC01_service_referencing_another_service_fails() =>
        Assert.Contains(CheckWith(P("Dalan.Relay", ProjectKind.Service, "Dalan.Identity")), e => e.StartsWith("ARC-01", StringComparison.Ordinal));

    [Fact]
    [Trait("Rule", "ARC-02")]
    public void ARC02_contracts_referencing_an_implementation_fails() =>
        Assert.Contains(CheckWith(P("Dalan.Relay.Contracts", ProjectKind.Contracts, "Dalan.Identity")), e => e.StartsWith("ARC-02", StringComparison.Ordinal));

    [Fact]
    [Trait("Rule", "ARC-03")]
    public void ARC03_relay_referencing_telegram_fails() =>
        Assert.Contains(CheckWith(P("Dalan.Relay", ProjectKind.Service, "Dalan.Telegram")), e => e.StartsWith("ARC-03", StringComparison.Ordinal));

    [Fact]
    [Trait("Rule", "ARC-03")]
    public void ARC03_relay_types_do_not_mention_telegram()
    {
        Assembly[] relay = [typeof(Dalan.Relay.RelayService).Assembly, Assembly.Load("Dalan.Relay.Contracts")];
        var bad = relay.SelectMany(a => a.GetTypes())
            .Where(t => (t.FullName ?? "").Contains("Telegram", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(bad);
    }

    [Fact]
    [Trait("Rule", "ARC-04")]
    public void ARC04_plugin_referencing_a_service_fails() =>
        Assert.Contains(CheckWith(P("Dalan.Telegram", ProjectKind.Plugin, "Dalan.Relay")), e => e.StartsWith("ARC-04", StringComparison.Ordinal));

    [Fact]
    [Trait("Rule", "ARC-05")]
    public void ARC05_non_host_referencing_a_plugin_fails() =>
        Assert.Contains(CheckWith(P("Dalan.Identity", ProjectKind.Service, "Dalan.Telegram")), e => e.StartsWith("ARC-05", StringComparison.Ordinal));

    [Fact]
    [Trait("Rule", "ARC-01")]
    [Trait("Rule", "ARC-02")]
    [Trait("Rule", "ARC-03")]
    [Trait("Rule", "ARC-04")]
    [Trait("Rule", "ARC-05")]
    public void Real_solution_follows_all_rules() => Assert.Empty(ArchitectureRules.Check(ProjectGraph.Load()));

    [Fact]
    [Trait("Rule", "ARC-06")]
    public void ARC06_agent_has_no_project_or_package_reference()
    {
        var agent = System.Xml.Linq.XDocument.Load(Path.Combine(ProjectGraph.RepoRoot(), "agent", "Dalan.Agent", "Dalan.Agent.csproj"));

        Assert.Empty(agent.Descendants("ProjectReference"));
        Assert.Empty(agent.Descendants("PackageReference"));
    }

    [Fact]
    [Trait("Rule", "ARC-06")]
    public void ARC06_no_server_project_references_the_agent() =>
        Assert.DoesNotContain(ProjectGraph.Load(), p => p.References.Contains("Dalan.Agent"));
}
