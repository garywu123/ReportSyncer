// ============================================================================
// File: CliArgumentParser.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Minimal CLI argument parser for --config and --dry-run.
// ============================================================================

using System.Diagnostics.CodeAnalysis;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Parses command-line arguments into structured options.
/// </summary>
/// <remarks>
/// <para>
/// Supported arguments:
/// <list type="bullet">
///   <item><c>--config &lt;path&gt;</c> or <c>--config=&lt;path&gt;</c></item>
///   <item><c>--dry-run</c>, <c>--dry-run true</c>, <c>--dry-run=false</c></item>
/// </list>
/// </para>
/// <para>
/// Any unknown argument will result in a parse failure with a usage error message.
/// </para>
/// </remarks>
public static class CliArgumentParser
{
    private const string ConfigArg = "--config";
    private const string DryRunArg = "--dry-run";

    /// <summary>
    /// Parses the command-line arguments into <see cref="CliRunOptions"/>.
    /// </summary>
    /// <param name="args">The command-line arguments array.</param>
    /// <returns>
    /// A tuple indicating success or failure:
    /// <list type="bullet">
    ///   <item><c>Ok</c>: true if parsing succeeded, false otherwise.</item>
    ///   <item><c>Options</c>: the parsed options if successful, otherwise null.</item>
    ///   <item><c>Error</c>: an error message if parsing failed, otherwise null.</item>
    /// </list>
    /// </returns>
    /// <example>
    /// <code><![CDATA[
    /// var (ok, options, error) = CliArgumentParser.TryParse(args);
    /// if (!ok)
    /// {
    ///     Console.Error.WriteLine(error);
    ///     return 1;
    /// }
    /// // Use options...
    /// ]]></code>
    /// </example>
    public static (bool Ok, CliRunOptions? Options, string? Error) TryParse(string[] args)
    {
        string? configPath = null;
        bool? dryRun = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith(ConfigArg, StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseKeyValue(arg, ConfigArg, out var value))
                {
                    configPath = value;
                }
                else
                {
                    // Next token is the value
                    if (i + 1 >= args.Length)
                    {
                        return (false, null, $"Missing value for {ConfigArg}. Usage: {GetUsage()}");
                    }
                    configPath = args[++i];
                }

                if (string.IsNullOrWhiteSpace(configPath))
                {
                    return (false, null, $"{ConfigArg} cannot be empty. Usage: {GetUsage()}");
                }
            }
            else if (arg.StartsWith(DryRunArg, StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseKeyValue(arg, DryRunArg, out var value))
                {
                    // --dry-run=value
                    if (!TryParseBool(value, out var parsed))
                    {
                        return (false, null, $"Invalid value '{value}' for {DryRunArg}. Expected 'true' or 'false'. Usage: {GetUsage()}");
                    }
                    dryRun = parsed;
                }
                else
                {
                    // Check if next token is a boolean value
                    if (i + 1 < args.Length && TryParseBool(args[i + 1], out var parsed))
                    {
                        dryRun = parsed;
                        i++;
                    }
                    else
                    {
                        // Just --dry-run with no value means true
                        dryRun = true;
                    }
                }
            }
            else
            {
                return (false, null, $"Unknown argument '{arg}'. Usage: {GetUsage()}");
            }
        }

        return (true, new CliRunOptions(configPath, dryRun), null);
    }

    /// <summary>
    /// Tries to parse a key=value format from an argument string.
    /// </summary>
    private static bool TryParseKeyValue(string arg, string key, [NotNullWhen(true)] out string? value)
    {
        if (arg.Length > key.Length && arg[key.Length] == '=')
        {
            value = arg.Substring(key.Length + 1);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Tries to parse a boolean value from a string.
    /// </summary>
    private static bool TryParseBool(string value, out bool result)
    {
        return bool.TryParse(value, out result);
    }

    /// <summary>
    /// Gets the usage message for CLI arguments.
    /// </summary>
    private static string GetUsage()
    {
        return $"Supported arguments: {ConfigArg} <path>, {DryRunArg} [true|false]";
    }
}
