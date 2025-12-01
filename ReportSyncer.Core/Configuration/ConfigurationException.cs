// ============================================================================
// File: ConfigurationException.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Exception type used for configuration loading and validation errors.
// ============================================================================

using System;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Represents an error that occurred while loading or parsing configuration.
    /// Hosts should catch this type to map to user-friendly error codes / HTTP responses.
    /// </summary>
    public class ConfigurationException : Exception
    {
        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationException"/> with a message.
        /// </summary>
        public ConfigurationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationException"/> with a message and inner exception.
        /// </summary>
        public ConfigurationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
