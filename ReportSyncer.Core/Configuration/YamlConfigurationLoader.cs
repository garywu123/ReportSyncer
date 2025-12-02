// ============================================================================
// File: YamlConfigurationLoader.cs
// Author: Gary Wu
// Date: 2025-12-02
// Project: ReportSyncer
// Description: YAML-based implementation of IConfigurationLoader using YamlDotNet.
// ============================================================================

/*
Pseudocode / Plan (detailed):

- Add XML documentation to the entire file:
  - File-level header already present; update date to today (2025-12-02).
  - For class `YamlConfigurationLoader`:
    - Add <summary> describing responsibility.
    - Add <example> showing typical usage.
    - Add <remarks> about error wrapping and strict top-level key validation.
  - For constructor:
    - Add <summary> and <param> for ILogService.
  - For public method `LoadAsync`:
    - Add <summary>, <param> for path and CancellationToken, <returns>, and <exception> tags for ConfigurationException and ArgumentException.
    - Document behavior: reads file, validates top-level keys, deserializes DTO, maps to domain, and wraps unexpected exceptions.
  - For internal static helpers `ReadFileAsync`, `LoadRootNode`, `ValidateTopLevelKeys`:
    - Add <summary>, <param>, <returns>, and document thrown ConfigurationException for invalid YAML.
  - For private methods `DeserializeRootDtoAsync`, `MapToDomain`, `ParseEnumOrThrow`:
    - Add <summary>, <param>, <returns>, and note about YAML parse errors and configuration validation.
  - For DTO classes and region:
    - Add <summary> for the DTO region and each DTO class describing its role (intermediate mapping target for YAML).
    - Keep property docs minimal or omitted except where helpful.
  - Use CDATA inside <code> blocks in <example> or <remarks> per project doc rules.
- Preserve existing logic and signatures exactly.
- Ensure XML doc comments do not alter compile-time behavior.
- Keep comments concise yet sufficient for automated documentation generation and code readers.
*/

using DotNetToolkit.Logging;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable MemberCanBePrivate.Global

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Loads configuration from a YAML file and maps it to the domain configuration model.
    /// Performs strict top-level key checks and wraps parsing errors in <see cref="ConfigurationException"/>.
    /// </summary>
    /// <remarks>
    /// The loader validates that the YAML document has only the allowed top-level keys,
    /// deserializes into an intermediate DTO graph using YamlDotNet, and then converts the DTOs
    /// into the domain <see cref="SyncConfiguration"/> model. Parsing errors and structural
    /// validation errors are surfaced as <see cref="ConfigurationException"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Example usage:
    /// var loader = new YamlConfigurationLoader(logService);
    /// var config = await loader.LoadAsync("C:\\configs\\sync.yaml", CancellationToken.None);
    /// ]]>
    /// </code>
    /// </example>
    public class YamlConfigurationLoader : IConfigurationLoader
    {
        /// <summary>
        /// Allowed top-level YAML keys (case-insensitive).
        /// </summary>
        private static readonly IReadOnlySet<string> AllowedTopLevelKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "version",
                "run",
                "safety",
                "schemaPolicy",
                "connections",
                "syncJobs"
            };

        /// <summary>
        /// YamlDotNet deserializer configured to use camel-case naming and to ignore unmatched properties.
        /// </summary>
        private static readonly IDeserializer Deserializer =
            new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

        private readonly ILogService _log;

#pragma warning disable IDE0290
        /// <summary>
        /// Constructs a new instance of <see cref="YamlConfigurationLoader"/>.
        /// </summary>
        /// <param name="log">Logging service used to record parse errors and unexpected exceptions.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="log"/> is null.</exception>
        public YamlConfigurationLoader(ILogService log)
#pragma warning restore IDE0290
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Loads and validates the YAML configuration from the specified file path and maps it to <see cref="SyncConfiguration"/>.
        /// </summary>
        /// <param name="path">Absolute path to the YAML configuration file.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A populated <see cref="SyncConfiguration"/> instance reflecting the YAML document.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="ConfigurationException">
        /// Thrown for YAML parsing errors, validation failures (missing required fields or unknown top-level keys),
        /// or when an unexpected error occurs while loading the configuration.
        /// </exception>
        public async Task<SyncConfiguration> LoadAsync(string path, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            try
            {
                var yamlText = await ReadFileAsync(path, ct).ConfigureAwait(false);
                var rootNode = LoadRootNode(yamlText);
                ValidateTopLevelKeys(rootNode);
                var dto = await DeserializeRootDtoAsync(yamlText, ct).ConfigureAwait(false);
                return MapToDomain(dto);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _log.LogErrorAsync("Unexpected error loading configuration", ex, ct);
                throw new ConfigurationException(
                    $"Unexpected error loading configuration: {ex.Message}", ex
                );
            }
        }

        /// <summary>
        /// Reads the YAML configuration file contents asynchronously.
        /// </summary>
        /// <param name="path">Absolute path to the configuration file.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The file contents as a string.</returns>
        internal static Task<string> ReadFileAsync(string path, CancellationToken ct) =>
            File.ReadAllTextAsync(path, ct);

        /// <summary>
        /// Parses the YAML document and returns the root mapping node.
        /// </summary>
        /// <param name="yamlText">Raw YAML text.</param>
        /// <returns>Root mapping node.</returns>
        /// <exception cref="ConfigurationException">
        /// Thrown if the document is empty or the top-level node is not a mapping/object.
        /// </exception>
        internal static YamlMappingNode LoadRootNode(string yamlText)
        {
            var yaml = new YamlStream();
            using var reader = new StringReader(yamlText);
            yaml.Load(reader);

            if (yaml.Documents.Count == 0)
                throw new ConfigurationException("Configuration file is empty.");

            if (yaml.Documents[0].RootNode is not YamlMappingNode rootMapping)
                throw new ConfigurationException("Top-level YAML node must be a mapping/object.");

            return rootMapping;
        }

        /// <summary>
        /// Ensures the YAML document only includes allowed top-level keys.
        /// </summary>
        /// <param name="rootNode">Root mapping node to validate.</param>
        /// <exception cref="ConfigurationException">Thrown when unknown top-level keys are present.</exception>
        internal static void ValidateTopLevelKeys(YamlMappingNode rootNode)
        {
            var unknownTop = rootNode.Children
                .Select(kv => kv.Key)
                .OfType<YamlScalarNode>()
                .Select(n => n.Value)
                .Where(k => k != null && !AllowedTopLevelKeys.Contains(k))
                .ToArray();

            if (unknownTop.Length > 0)
            {
                throw new ConfigurationException(
                    $"Unknown top-level configuration keys: {string.Join(", ", unknownTop)}"
                );
            }
        }

        /// <summary>
        /// Deserializes the YAML text into the intermediate DTO model.
        /// </summary>
        /// <param name="yamlText">Raw YAML text.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The populated root DTO.</returns>
        /// <exception cref="ConfigurationException">Thrown when YamlDotNet fails to parse the document or deserialization returns null.</exception>
        private async Task<RootDto> DeserializeRootDtoAsync(string yamlText, CancellationToken ct)
        {
            try
            {
                using var reader = new StringReader(yamlText);
                return Deserializer.Deserialize<RootDto>(reader)
                       ?? throw new ConfigurationException("Failed to deserialize configuration file.");
            }
            catch (YamlDotNet.Core.YamlException ye)
            {
                await _log.LogErrorAsync("YAML parse error loading configuration", ye, ct);
                throw new ConfigurationException($"YAML parse error: {ye.Message}", ye);
            }
        }

        /// <summary>
        /// Converts the deserialized DTO graph into the domain configuration model.
        /// </summary>
        /// <param name="dto">Root DTO.</param>
        /// <returns>Domain configuration instance.</returns>
        /// <exception cref="ConfigurationException">
        /// Thrown when required fields are missing or enum values are invalid.
        /// </exception>
        private SyncConfiguration MapToDomain(RootDto dto)
        {
            if (dto == null)
                throw new ConfigurationException("Configuration document is null or malformed.");

            if (string.IsNullOrWhiteSpace(dto.Version))
                throw new ConfigurationException("Missing required field: version");

            if (dto.Run == null) throw new ConfigurationException("Missing required section: run");

            if (dto.Safety == null)
                throw new ConfigurationException("Missing required section: safety");

            if (dto.SchemaPolicy == null)
                throw new ConfigurationException("Missing required section: schemaPolicy");

            if (dto.Connections == null || dto.Connections.Count == 0)
                throw new ConfigurationException(
                    "At least one connection must be defined in 'connections'."
                );

            if (dto.SyncJobs == null || dto.SyncJobs.Count == 0)
                throw new ConfigurationException(
                    "At least one sync job must be defined in 'syncJobs'."
                );

            // Map Run
            var run = new RunConfig(
                dto.Run.DryRun,
                dto.Run.DefaultBatchSize,
                dto.Run.DeleteChunkSize,
                dto.Run.UseTvpIfAvailable,
                dto.Run.EtaSmoothing
            );

            // Map Safety
            var safety = new SafetyConfig(
                dto.Safety.ForbidProdToProd,
                dto.Safety.RequireDifferentConnections,
                dto.Safety.ConfirmLargeDeletePct
            );

            // Map SchemaPolicy
            var schemaPolicy = new SchemaPolicyConfig(
                ParseEnumOrThrow<SchemaMismatchBehavior>(
                    dto.SchemaPolicy.OnMismatch, "schemaPolicy.onMismatch"
                ),
                dto.SchemaPolicy.RequirePrimaryKey,
                dto.SchemaPolicy.AllowExtraTargetColumns
            );

            // Map connections
            var connections = dto.Connections.Select(c =>
                    {
                        if (string.IsNullOrWhiteSpace(c.Name))
                            throw new ConfigurationException("Connection entry missing 'name'.");

                        if (string.IsNullOrWhiteSpace(c.ConnectionString))
                            throw new ConfigurationException(
                                $"Connection '{c.Name}' missing 'connectionString'."
                            );

                        var env = ParseEnumOrThrow<EnvironmentType>(
                            c.Environment, $"connections[{c.Name}].environment"
                        );

                        var type = ParseEnumOrThrow<ConnectionType>(
                            c.Type, $"connections[{c.Name}].type"
                        );

                        return new ConnectionConfig(c.Name, c.ConnectionString, env, type);
                    }
                )
               .ToArray();

            // Map jobs
            var jobs = dto.SyncJobs.Select(j =>
                    {
                        if (string.IsNullOrWhiteSpace(j.Name))
                            throw new ConfigurationException("Sync job missing 'name'.");

                        if (string.IsNullOrWhiteSpace(j.SourceConnection))
                            throw new ConfigurationException(
                                $"Job '{j.Name}' missing 'sourceConnection'."
                            );

                        if (string.IsNullOrWhiteSpace(j.TargetConnection))
                            throw new ConfigurationException(
                                $"Job '{j.Name}' missing 'targetConnection'."
                            );

                        var parameters = j.Parameters != null
                            ? new Dictionary<string, string>(j.Parameters)
                            : new Dictionary<string, string>();

                        if (j.Tables == null || j.Tables.Count == 0)
                            throw new ConfigurationException(
                                $"Job '{j.Name}' must contain at least one table in 'tables'."
                            );

                        var tables = j.Tables.Select(t =>
                                {
                                    if (string.IsNullOrWhiteSpace(t.Source))
                                        throw new ConfigurationException(
                                            $"Job '{j.Name}' has a table with missing 'source'."
                                        );

                                    if (string.IsNullOrWhiteSpace(t.Target))
                                        throw new ConfigurationException(
                                            $"Job '{j.Name}' has a table with missing 'target'."
                                        );

                                    FilterConfig? filter = null;
                                    if (t.Filter != null)
                                    {
                                        filter = new FilterConfig(
                                            t.Filter.DateColumn,
                                            t.Filter.StartDate,
                                            t.Filter.EndDate,
                                            t.Filter.KeyColumn,
                                            t.Filter.Value
                                        );
                                    }

                                    ColumnMappingConfig? mapping = null;
                                    var added = t.ColumnMapping?.AddedColumns?.Select(a
                                        => a is { ColumnName: not null, Value: not null }
                                            ? new AddedColumnMappingConfig(a.ColumnName, a.Value)
                                            : null
                                    );

                                    if (added != null)
                                        mapping = new ColumnMappingConfig(
                                            t.ColumnMapping is { AutomapByName: true },
                                            t.ColumnMapping?.ExplicitMappings != null
                                                ? new Dictionary<string, string>(
                                                    t.ColumnMapping.ExplicitMappings
                                                )
                                                : null,
                                            added!
                                        );

                                    KeyConfig? keys = null;
                                    if (t.Keys != null && t.Keys.BusinessKey != null)
                                    {
                                        keys = new KeyConfig(t.Keys.BusinessKey);
                                    }

                                    SyncOptionsConfig? options = null;
                                    if (t.SyncOptions != null)
                                    {
                                        options = new SyncOptionsConfig(
                                            t.SyncOptions.BatchSize, t.SyncOptions.UseTvp
                                        );
                                    }

                                    return new TableTaskConfig(
                                        source: t.Source,
                                        target: t.Target,
                                        enabled: t.Enabled.GetValueOrDefault(true),
                                        preSyncTargetAction: t.PreSyncTargetAction.GetValueOrDefault(
                                            false
                                        ),
                                        allowAllDelete: t.AllowAllDelete.GetValueOrDefault(false),
                                        enableIdentityInsert: t.EnableIdentityInsert.GetValueOrDefault(
                                            false
                                        ),
                                        filter: filter,
                                        columnMapping: mapping,
                                        keys: keys,
                                        syncOptions: options
                                    );
                                }
                            )
                           .ToArray();

                        return new SyncJobConfig(
                            j.Name, j.Description, j.SourceConnection, j.TargetConnection, parameters,
                            tables
                        );
                    }
                )
               .ToArray();

            return new SyncConfiguration(
                dto.Version, run, safety, schemaPolicy, connections,
                jobs
            );
        }

        /// <summary>
        /// Parses a string into an enum of type <typeparamref name="TEnum"/> or throws a <see cref="ConfigurationException"/>.
        /// </summary>
        /// <typeparam name="TEnum">Enum type to parse.</typeparam>
        /// <param name="value">String value to parse.</param>
        /// <param name="fieldPath">Configuration field path used for error messages.</param>
        /// <returns>Parsed enum value.</returns>
        /// <exception cref="ConfigurationException">Thrown when the value is null/whitespace or not a valid enum name.</exception>
        private static TEnum ParseEnumOrThrow<TEnum>(string? value, string fieldPath)
            where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ConfigurationException($"Missing required enum value for '{fieldPath}'.");

            if (Enum.TryParse<TEnum>(value, true, out var parsed)) return parsed;

            throw new ConfigurationException(
                $"Invalid value '{
                    value
                }' for '{
                    fieldPath
                }'. Expected one of: {
                    string.Join(", ", Enum.GetNames(typeof(TEnum)))
                }"
            );
        }

        #region DTOs

        /// <summary>
        /// DTO classes used as intermediate targets for YAML deserialization.
        /// These types mirror the expected YAML structure and are not part of the domain model.
        /// </summary>
        /// <remarks>
        /// The DTOs are intentionally internal and minimal; domain validation and mapping happen
        /// in <see cref="MapToDomain(RootDto)"/>.
        /// </remarks>
        private class RootDto
        {
            public string?              Version      { get; set; }
            public RunDto?              Run          { get; set; }
            public SafetyDto?           Safety       { get; set; }
            public SchemaPolicyDto?     SchemaPolicy { get; set; }
            public List<ConnectionDto>? Connections  { get; set; }
            public List<SyncJobDto>?    SyncJobs     { get; set; }
        }

        private class RunDto
        {
            public bool    DryRun            { get; set; }
            public int     DefaultBatchSize  { get; set; }
            public int     DeleteChunkSize   { get; set; }
            public bool    UseTvpIfAvailable { get; set; }
            public double? EtaSmoothing      { get; set; }
        }

        private class SafetyDto
        {
            public bool   ForbidProdToProd            { get; set; }
            public bool   RequireDifferentConnections { get; set; }
            public double ConfirmLargeDeletePct       { get; set; }
        }

        private class SchemaPolicyDto
        {
            public string? OnMismatch              { get; set; }
            public bool    RequirePrimaryKey       { get; set; }
            public bool    AllowExtraTargetColumns { get; set; }
        }

        private class ConnectionDto
        {
            public string? Name             { get; set; }
            public string? ConnectionString { get; set; }
            public string? Environment      { get; set; }
            public string? Type             { get; set; }
        }

        private class SyncJobDto
        {
            public string?                     Name             { get; set; }
            public string?                     Description      { get; set; }
            public string?                     SourceConnection { get; set; }
            public string?                     TargetConnection { get; set; }
            public Dictionary<string, string>? Parameters       { get; set; }
            public List<TableDto>?             Tables           { get; set; }
        }

        private class TableDto
        {
            public string?           Source               { get; set; }
            public string?           Target               { get; set; }
            public bool?             Enabled              { get; set; }
            public bool?             PreSyncTargetAction  { get; set; }
            public bool?             AllowAllDelete       { get; set; }
            public bool?             EnableIdentityInsert { get; set; }
            public FilterDto?        Filter               { get; set; }
            public ColumnMappingDto? ColumnMapping        { get; set; }
            public KeyDto?           Keys                 { get; set; }
            public SyncOptionsDto?   SyncOptions          { get; set; }
        }

        private class FilterDto
        {
            public string? DateColumn { get; set; }
            public string? StartDate  { get; set; }
            public string? EndDate    { get; set; }
            public string? KeyColumn  { get; set; }
            public string? Value      { get; set; }
        }

        private class ColumnMappingDto
        {
            public bool                        AutomapByName    { get; set; } = true;
            public Dictionary<string, string>? ExplicitMappings { get; set; }
            // ReSharper disable once CollectionNeverUpdated.Local
            public List<AddedColumnDto>?       AddedColumns     { get; set; }
        }

        private class AddedColumnDto
        {
            public string? ColumnName { get; set; }
            public string? Value      { get; set; }
        }

        private class KeyDto
        {
            public List<string>? BusinessKey { get; set; }
        }

        private class SyncOptionsDto
        {
            public int?  BatchSize { get; set; }
            public bool? UseTvp    { get; set; }
        }

        #endregion
    }
}
