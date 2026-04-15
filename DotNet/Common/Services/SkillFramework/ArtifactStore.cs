using System.Collections.Concurrent;

namespace CXOAI.SkillFramework;

public class Artifact
{
    public required string Key { get; init; }
    public required string SkillName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public long Size => Data.Length;
}

public class ArtifactStore
{
    private readonly ConcurrentDictionary<string, Artifact> _artifacts = new();

    public string Store(string skillName, string key, byte[] data, string contentType)
    {
        var artifactKey = $"{skillName}/{key}";
        _artifacts[artifactKey] = new Artifact
        {
            Key = artifactKey,
            SkillName = skillName,
            ContentType = contentType,
            Data = data
        };
        return $"artifact://{artifactKey}";
    }

    public Artifact? Get(string artifactKey)
    {
        _artifacts.TryGetValue(artifactKey, out var artifact);
        return artifact;
    }

    public IReadOnlyDictionary<string, Artifact> GetBySkill(string skillName)
    {
        return _artifacts
            .Where(kv => kv.Value.SkillName.Equals(skillName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public IReadOnlyDictionary<string, Artifact> All => _artifacts;
}
