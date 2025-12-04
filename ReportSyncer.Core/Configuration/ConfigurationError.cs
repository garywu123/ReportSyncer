// ============================================================================
// File: ConfigurationError.cs
// Author: Gary Wu
// Date: 2025-12-02
// Project: ReportSyncer
// Description: Represents a single configuration validation error with code, message, and path.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Represents a single configuration validation error.
/// </summary>
/// <remarks>
/// This record type encapsulates configuration validation failures, including
/// error code for programmatic handling, human-readable message, and the path
/// within the configuration tree where the error occurred.
/// </remarks>
public sealed record ConfigurationError
{
    /// <summary>
    /// Gets the error code used for programmatic identification and host-layer mapping.
    /// </summary>
    /// <remarks>
    /// Error codes follow the pattern <c>CFG_&lt;DOMAIN&gt;_&lt;ISSUE&gt;</c>, e.g.
    /// <c>CFG_CONNECTIONS_EMPTY</c>, <c>CFG_CONNECTION_NAME_DUPLICATE</c>.
    /// These codes are stable for testing and host integration.
    /// </remarks>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    /// <remarks>
    /// This message is intended for end-user consumption and should clearly
    /// describe what is wrong and how to fix it.
    /// </remarks>
    public string Message { get; }

    /// <summary>
    /// Gets the path within the configuration structure where the error occurred, or null if not applicable.
    /// </summary>
    /// <remarks>
    /// Path examples:
    /// <list type="bullet">
    /// <item><description><c>"connections"</c> - root-level collection issue</description></item>
    /// <item><description><c>"connections[0]"</c> - first connection object</description></item>
    /// <item><description><c>"jobs[MyJob].tables[0]"</c> - specific table in a job</description></item>
    /// <item><description><c>"run.batchSize"</c> - specific property</description></item>
    /// </list>
    /// </remarks>
    public string? Path { get; }

    /// <summary>
    /// Creates a new <see cref="ConfigurationError"/>.
    /// </summary>
    /// <param name="code">The error code (required, non-empty).</param>
    /// <param name="message">The human-readable error message (required, non-empty).</param>
    /// <param name="path">Optional path within the configuration tree.</param>
    public ConfigurationError(string code, string message, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Error code cannot be empty or whitespace.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message cannot be empty or whitespace.", nameof(message));

        Code = code;
        Message = message;
        Path = path;
    }
}
