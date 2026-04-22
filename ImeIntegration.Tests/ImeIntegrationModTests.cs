using System.Reflection;
using ImeIntegration;
using Xunit;

namespace ImeIntegration.Tests;

public sealed class ImeIntegrationModTests
{
    [Fact]
    public void Public_metadata_matches_project_properties()
    {
        var mod = new ImeIntegrationMod();
        var assembly = typeof(ImeIntegrationMod).Assembly;
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
        var author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var repositoryUrl = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(meta => meta.Key == "RepositoryUrl");

        Assert.Equal(ProjectPropertiesReader.ExpectedAssemblyTitle, mod.Name);
        Assert.Equal(ProjectPropertiesReader.ExpectedAuthor, mod.Author);
        Assert.False(string.IsNullOrWhiteSpace(mod.Version));
        Assert.Equal(ProjectPropertiesReader.ExpectedRepositoryUrl, mod.Link);

        Assert.False(string.IsNullOrWhiteSpace(title?.Title));
        Assert.False(string.IsNullOrWhiteSpace(author?.Company));
        Assert.False(string.IsNullOrWhiteSpace(version?.InformationalVersion));
        Assert.False(string.IsNullOrWhiteSpace(repositoryUrl?.Value));
    }
}
