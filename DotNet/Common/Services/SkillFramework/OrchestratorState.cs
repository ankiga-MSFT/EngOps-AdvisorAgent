namespace CXOAI.SkillFramework;

/// <summary>
/// In-memory orchestrator state store that mimics Azure Durable Functions activity output caching.
/// Each activity stores its output here. On replay (or re-entry), the orchestrator checks
/// if the output already exists and skips the activity call � exactly like Durable Functions replay.
///
/// Durable Functions mapping:
///   Console (this class)                    ? Durable Functions
///   ??????????????????????????????????????? ? ??????????????????????????????????
///   state.Set("key", value)                 ? ctx.CallActivityAsync stores result in history
///   state.TryGet("key", out value)          ? orchestrator replay reads from history
///   state.Clear()                           ? new orchestration instance
///
/// When migrating to Durable Functions, replace this with orchestrator context state:
///   var result = await ctx.CallActivityAsync&lt;T&gt;("ActivityName", input);
///   // Durable Functions automatically replays completed activities from history
/// </summary>
public class OrchestratorState
{
    private readonly Dictionary<string, object> _state = new(StringComparer.OrdinalIgnoreCase);

    public void Set<T>(string key, T value) where T : notnull
    {
        _state[key] = value;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_state.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public T Get<T>(string key) => _state.TryGetValue(key, out var obj) && obj is T typed
        ? typed
        : throw new KeyNotFoundException($"Orchestrator state key '{key}' not found or wrong type.");

    public bool Has(string key) => _state.ContainsKey(key);

    public void Clear() => _state.Clear();
}
