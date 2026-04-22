using System.Xml.Linq;

namespace ResoniteImeIntegration.Tests;

/// <summary>
/// Helper class to read properties from MSBuild project files.
/// </summary>
internal static class ProjectPropertiesReader
{
    private static readonly Lazy<string> SolutionDirectory = new(ResolveSolutionDirectory);
    private static readonly Lazy<XDocument> DirectoryBuildProps = new(LoadDirectoryBuildProps);

    /// <summary>
    /// Reads a property value from Directory.Build.props file.
    /// </summary>
    /// <param name="propertyName">The name of the property to read.</param>
    /// <returns>The property value, or null if not found.</returns>
    public static string? GetPropertyFromDirectoryBuildProps(string propertyName)
    {
        try
        {
            XElement? propertyElement = DirectoryBuildProps
                .Value
                .Descendants(propertyName)
                .FirstOrDefault();
            string? value = propertyElement?.Value;

            return value;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to read property '{propertyName}' from Directory.Build.props: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Reads a required property value from Directory.Build.props file.
    /// </summary>
    /// <param name="propertyName">The name of the property to read.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the property is missing.</exception>
    public static string GetRequiredPropertyFromDirectoryBuildProps(string propertyName)
    {
        string? value = GetPropertyFromDirectoryBuildProps(propertyName);
        return value ?? throw new InvalidOperationException(
            $"{propertyName} property not found in Directory.Build.props"
        );
    }

    /// <summary>
    /// Gets the expected repository URL from Directory.Build.props.
    /// </summary>
    public static string ExpectedRepositoryUrl =>
        GetRequiredPropertyFromDirectoryBuildProps("RepositoryUrl");

    /// <summary>
    /// Gets the expected author from Directory.Build.props.
    /// </summary>
    public static string ExpectedAuthor =>
        GetRequiredPropertyFromDirectoryBuildProps("Authors");

    /// <summary>
    /// Gets the expected version from Directory.Build.props or GitVersion configuration.
    /// </summary>
    public static string ExpectedVersion =>
        GetPropertyFromDirectoryBuildProps("Version") ?? GetGitVersionMarker();

    /// <summary>
    /// Gets the expected assembly title (project name) by deriving it from the solution file name.
    /// </summary>
    public static string ExpectedAssemblyTitle
    {
        get
        {
            // Derive project name from solution file
            string solutionDir = SolutionDirectory.Value;
            string? solutionFile = Directory
                .GetFiles(solutionDir, "*.sln")
                .FirstOrDefault();
            return solutionFile == null
                ? throw new InvalidOperationException("No solution file found in solution directory")
                : Path.GetFileNameWithoutExtension(solutionFile);
        }
    }

    private static string ResolveSolutionDirectory()
    {
        string? testDirectory = Path.GetDirectoryName(
            typeof(ProjectPropertiesReader).Assembly.Location
        );
        return Directory.GetParent(testDirectory!)?.Parent?.Parent?.FullName
            ?? throw new InvalidOperationException("Could not determine solution directory");
    }

    private static XDocument LoadDirectoryBuildProps()
    {
        string path = Path.Combine(SolutionDirectory.Value, "Directory.Build.props");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Directory.Build.props not found at: {path}");
        }

        return XDocument.Load(path);
    }

    private static string GetGitVersionMarker()
    {
        string gitVersionPath = Path.Combine(SolutionDirectory.Value, "GitVersion.yml");
        if (!File.Exists(gitVersionPath))
        {
            throw new InvalidOperationException("GitVersion.yml not found in solution root");
        }

        string? firstLine = File.ReadLines(gitVersionPath)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(firstLine)
            ? throw new InvalidOperationException("GitVersion.yml is empty")
            : firstLine.Trim();
    }
}
