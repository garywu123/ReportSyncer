// ============================================================================
// File: ConfigurationException.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Exception type used for configuration loading and validation errors.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Represents one or more errors that occurred while loading, parsing, or validating configuration.
/// Hosts should catch this type to map errors to user-friendly responses (exit codes, HTTP status, etc.).
/// </summary>
/// <remarks>
/// This exception aggregates multiple <see cref="ConfigurationError"/> instances to allow
/// clients to see all configuration problems in a single validation pass, rather than
/// failing on the first error.
/// </remarks>
public class ConfigurationException : Exception
{
    /// <summary>
    /// Gets the collection of configuration errors.
    /// </summary>
    /// <remarks>
    /// This collection is never empty. If no errors exist, the exception is not thrown.
    /// </remarks>
    public IReadOnlyList<ConfigurationError> Errors { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ConfigurationException"/> with a single error message.
    /// </summary>
    /// <remarks>
    /// This constructor is provided for backward compatibility and simple error cases.
    /// For validation errors, prefer using the overload that accepts a collection of <see cref="ConfigurationError"/>.
    /// </remarks>
    public ConfigurationException(string message)
        : base(message)
    {
        Errors = new List<ConfigurationError> { new ConfigurationError("CFG_UNKNOWN", message) };
    }

    /// <summary>
    /// Creates a new instance of <see cref="ConfigurationException"/> with a message and inner exception.
    /// </summary>
    /// <remarks>
    /// This constructor is provided for backward compatibility.
    /// For validation errors, prefer using the overload that accepts a collection of <see cref="ConfigurationError"/>.
    /// </remarks>
    public ConfigurationException(string message, Exception inner)
        : base(message, inner)
    {
        Errors = new List<ConfigurationError> { new ConfigurationError("CFG_UNKNOWN", message) };
    }

    /// <summary>
    /// Creates a new instance of <see cref="ConfigurationException"/> with a collection of configuration errors.
    /// </summary>
    /// <param name="errors">Collection of configuration errors (must not be empty).</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="errors"/> is empty.</exception>
    public ConfigurationException(IEnumerable<ConfigurationError> errors)
        : base(BuildMessage(errors))
    {
        ArgumentNullException.ThrowIfNull(errors);

        var errorList = errors.ToList();
        if (errorList.Count == 0)
            throw new ArgumentException("Configuration exception must contain at least one error.", nameof(errors));

        Errors = errorList.AsReadOnly();
    }

    /// <summary>
    /// Builds a summary message from a collection of configuration errors.
    /// </summary>
    private static string BuildMessage(IEnumerable<ConfigurationError> errors)
    {
        if (errors == null)
            return "Configuration validation failed.";

        var errorList = errors.ToList();
        if (errorList.Count == 0)
            return "Configuration validation failed.";

        if (errorList.Count == 1)
            return $"Configuration validation failed: {errorList[0].Message} (at {errorList[0].Path ?? "root"})";

        var summary = $"Configuration validation failed with {errorList.Count} error(s):\n";
        summary += string.Join("\n", errorList.Select((e, i) =>
            $"  [{i + 1}] {e.Code}: {e.Message} (at {e.Path ?? "root"})"));

        return summary;
    }
}