// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Build;

/// <summary>Human-readable record of everything a build changed, inferred and checked.</summary>
public sealed class BuildReport
{
    readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public void Add(string line) => _lines.Add(line);
    public void Add(IEnumerable<string> lines) => _lines.AddRange(lines);
    public void Section(string title) => _lines.Add($"## {title}");

    public string ToMarkdown()
    {
        var body = string.Join("\n", _lines.Select(l => l.StartsWith("## ") ? $"\n{l}\n" : $"- {l}"));
        return $"# am4ever build report\n\n{body}\n";
    }
}

/// <summary>The output of a build: the new image and its report.</summary>
public sealed record BuildResult(byte[] Image, BuildReport Report);
