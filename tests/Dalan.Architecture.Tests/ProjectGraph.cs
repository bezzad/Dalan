using System.Xml.Linq;

namespace Dalan.Architecture.Tests;

public enum ProjectKind { Host, Service, Contracts, Plugin }

public sealed record Project(string Name, ProjectKind Kind, IReadOnlyList<string> References);

/// <summary>Reads the project reference graph of everything under src/ from the .csproj files.</summary>
public static class ProjectGraph
{
    public static IReadOnlyList<Project> Load()
    {
        var src = Path.Combine(RepoRoot(), "src");
        return Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(path => new Project(
                Path.GetFileNameWithoutExtension(path),
                KindOf(path, src),
                XDocument.Load(path).Descendants("ProjectReference")
                    .Select(r => Path.GetFileNameWithoutExtension((string)r.Attribute("Include")!))
                    .ToList()))
            .ToList();
    }

    public static ProjectKind KindOf(string path, string src)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name == "Dalan.Host") return ProjectKind.Host;
        if (name.EndsWith(".Contracts", StringComparison.Ordinal)) return ProjectKind.Contracts;
        if (Path.GetRelativePath(src, path).StartsWith("Plugins", StringComparison.Ordinal)) return ProjectKind.Plugin;
        return ProjectKind.Service;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dalan.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Dalan.slnx not found above the test folder.");
    }
}
