using ReportSyncer.Core.Configuration;


namespace ReportSyncer.Core.Tests.Helpers;

/// <summary>
/// Helper to load YAML test files using the production YAML loader.
/// </summary>
public static class ConfigurationYamlTestHelper
{
    /// <summary>
    /// Load a test YAML file synchronously from the test output `Configuration/TestFiles` folder.
    /// </summary>
    /// <param name="relativePath">File name under `Configuration/TestFiles` (e.g. "valid-minimal.yml").</param>
    public static SyncConfiguration LoadFromYamlFile(string relativePath)
        => LoadFromYamlFileAsync(relativePath).GetAwaiter().GetResult();

    /// <summary>
    /// Async loader wrapper for tests.
    /// </summary>
    private static async Task<SyncConfiguration> LoadFromYamlFileAsync(string relativePath, CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        var path = Path.Combine(baseDir, "TestFiles", relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Test YAML not found: {path}", path);

        var loader = new YamlConfigurationLoader(new DummyLogService());
        return await loader.LoadAsync(path, ct).ConfigureAwait(false);
    }
}
