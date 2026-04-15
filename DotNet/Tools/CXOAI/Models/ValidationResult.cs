namespace CXOAI.Tools.Models;

/// <summary>
/// Generic validation result used by inline guardrail methods.
/// </summary>
public class ValidationResult<T> where T : class
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public T? ValidatedResult { get; set; }

    public static ValidationResult<T> Ok(T result) => new() { Success = true, ValidatedResult = result };

    public static ValidationResult<T> Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Result of date range resolution — either explicit (YYYY-MM-DD) or relative ("last 6 months").
/// </summary>
public class DateResolutionResult
{
    public bool Success { get; set; }
    public DateTime? ResolvedStartDate { get; set; }
    public DateTime? ResolvedEndDate { get; set; }
    public string? Error { get; set; }

    public static DateResolutionResult Ok(DateTime start, DateTime end) => new()
    {
        Success = true,
        ResolvedStartDate = start,
        ResolvedEndDate = end
    };

    public static DateResolutionResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
