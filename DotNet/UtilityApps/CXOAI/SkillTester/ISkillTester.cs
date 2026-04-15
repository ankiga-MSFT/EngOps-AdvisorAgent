using CXOAI.ConfigurationStore;

namespace CXOAI.SkillTester;

/// <summary>
/// Common interface for all skill testers. Program.cs resolves the selected
/// tester and calls <see cref="RunAsync"/> — no switch on concrete types needed.
/// The patcher uses <see cref="GetSkillConfiguration"/> to sync all config fields
/// from the tester class into Skills.json.
/// </summary>
public interface ISkillTester
{
    string Name { get; }
    Task RunAsync();

    /// <summary>
    /// Returns the complete skill configuration that should be serialized into
    /// the Configuration field of Skills.json. Every tester defines all fields
    /// (SystemPrompt, ModelName, ExpectedSkillInput, Temperature, Seed, Timeout, Type)
    /// so the patcher can rebuild the entire stringified JSON.
    /// </summary>
    SkillConfiguration GetSkillConfiguration();
}
