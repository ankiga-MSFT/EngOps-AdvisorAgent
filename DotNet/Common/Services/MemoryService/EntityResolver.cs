namespace CXOAI.Memory;

/// <summary>
/// Lightweight entity resolver that matches prompt tokens against known entities
/// from the UI payload (current entity, favorites, recents).
///
/// Resolution chain:
///   Step 1: Match current entity → keep as-is
///   Step 2: Match scoped dictionary (same EntityType) → override
///   Step 3: Cross-type fallback (other EntityTypes) → override
///   Step 4: No match → keep current as-is
///
/// Pure static utility — no DI, no LLM calls, deterministic.
/// </summary>
public static class EntityResolver
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "show", "me", "my", "get", "give", "for", "of", "the", "a", "an",
        "and", "or", "in", "to", "with", "by", "from", "on", "at", "is",
        "are", "was", "what", "how", "their", "its", "this", "that", "it",
        "all", "do", "does", "did", "will", "can", "could", "would", "should",
        "please", "tell", "about", "be", "been", "being", "have", "has", "had",
        "compare", "export", "send", "email", "generate", "create", "download",
        "summary", "quick", "detailed", "report"
    };

    /// <summary>
    /// Resolves the best matching entity from the prompt against the UI payload.
    /// Returns null if no match found (caller should keep current entity).
    /// Returns the matched EntityReference if a better match is found.
    /// </summary>
    public static EntityReference? Resolve(string prompt, UIPayload payload)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return null;

        var promptTokens = Tokenize(prompt);
        if (promptTokens.Count == 0)
            return null;

        // Step 1: Match current entity
        var currentEntity = BuildCurrentEntity(payload);
        if (currentEntity != null && HasTokenMatch(currentEntity.EntityName, promptTokens))
            return null; // Current entity matches — caller keeps as-is

        var entityType = (payload.EntityType ?? string.Empty).ToLowerInvariant();

        // Step 2: Match scoped dictionary (same EntityType)
        var scopedCandidates = GetScopedCandidates(payload, entityType);
        var scopedMatch = FindFirstMatch(scopedCandidates, promptTokens);
        if (scopedMatch != null)
            return scopedMatch;

        // Step 3: Cross-type fallback (only if EntityType is set — otherwise Step 2 already searched all)
        if (!string.IsNullOrEmpty(entityType))
        {
            var fallbackOrder = GetFallbackOrder(entityType);
            foreach (var fallbackType in fallbackOrder)
            {
                var fallbackCandidates = GetScopedCandidates(payload, fallbackType);
                var fallbackMatch = FindFirstMatch(fallbackCandidates, promptTokens);
                if (fallbackMatch != null)
                    return fallbackMatch;
            }
        }

        // Step 4: No match → return null (caller keeps current)
        return null;
    }

    /// <summary>
    /// Tokenizes text: space-delimited, lowercased, stop words removed.
    /// </summary>
    private static HashSet<string> Tokenize(string text)
    {
        return text.Split([' ', ',', '&', '(', ')', '/', '-', '?', '.', '!', '"', '\'', '%'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if ANY non-stopword token from the entity name appears in prompt tokens.
    /// </summary>
    private static bool HasTokenMatch(string entityName, HashSet<string> promptTokens)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return false;

        var entityTokens = Tokenize(entityName);
        return entityTokens.Any(t => promptTokens.Contains(t));
    }

    /// <summary>
    /// Finds the first entity in the candidate list whose name has a token match with the prompt.
    /// Favorites come before recents in the list (caller controls order).
    /// </summary>
    private static EntityReference? FindFirstMatch(List<EntityReference> candidates, HashSet<string> promptTokens)
    {
        foreach (var candidate in candidates)
        {
            if (HasTokenMatch(candidate.EntityName, promptTokens))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Builds an EntityReference from the current entity fields in the payload.
    /// Returns null if no current entity is set.
    /// </summary>
    private static EntityReference? BuildCurrentEntity(UIPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.EntityName))
            return null;

        return new EntityReference
        {
            EntityName = payload.EntityName,
            EntityId = payload.EntityId ?? string.Empty,
            EntityType = payload.EntityType ?? string.Empty
        };
    }

    /// <summary>
    /// Returns candidates for the given entity type: favorites first, then recents (deduped).
    /// When entityType is empty, returns ALL favorites + recents across all types.
    /// </summary>
    private static List<EntityReference> GetScopedCandidates(UIPayload payload, string entityType)
    {
        var candidates = new List<EntityReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddRange(IEnumerable<EntityReference>? entities)
        {
            if (entities == null) return;
            foreach (var e in entities)
            {
                if (!string.IsNullOrWhiteSpace(e.EntityName) && seen.Add(e.EntityId))
                    candidates.Add(e);
            }
        }

        switch (entityType)
        {
            case "customer":
                AddRange(payload.FavoriteCustomers);
                AddRange(payload.RecentCustomers);
                break;
            case "product":
                AddRange(payload.FavoriteProducts);
                AddRange(payload.RecentProducts);
                break;
            case "program":
                AddRange(payload.FavoritePrograms);
                AddRange(payload.RecentPrograms);
                break;
            default: // empty — search all
                AddRange(payload.FavoriteCustomers);
                AddRange(payload.RecentCustomers);
                AddRange(payload.FavoriteProducts);
                AddRange(payload.RecentProducts);
                AddRange(payload.FavoritePrograms);
                AddRange(payload.RecentPrograms);
                break;
        }

        return candidates;
    }

    /// <summary>
    /// Returns the fallback entity types to search when the scoped search (Step 2) fails.
    /// Order: customers → products → programs (minus the current type).
    /// </summary>
    private static List<string> GetFallbackOrder(string currentEntityType)
    {
        var allTypes = new List<string> { "customer", "product", "program" };
        allTypes.Remove(currentEntityType);
        return allTypes;
    }
}
