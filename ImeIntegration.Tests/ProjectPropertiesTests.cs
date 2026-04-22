#pragma warning disable CS1591
#pragma warning disable CA1515 // Types should be made internal - xUnit requires test classes to be public
using Xunit;

namespace ImeIntegration.Tests;

public sealed class ProjectPropertiesTests
{
    [Fact]
    public void Debug_ProjectPropertiesReader()
    {
        string repositoryUrl = ProjectPropertiesReader.ExpectedRepositoryUrl;
        string author = ProjectPropertiesReader.ExpectedAuthor;
        string version = ProjectPropertiesReader.ExpectedVersion;
        string assemblyTitle = ProjectPropertiesReader.ExpectedAssemblyTitle;

        Assert.NotNull(repositoryUrl);
        Assert.NotEmpty(repositoryUrl);
        Assert.NotNull(author);
        Assert.NotEmpty(author);
        Assert.NotNull(version);
        Assert.NotEmpty(version);
        Assert.NotNull(assemblyTitle);
        Assert.NotEmpty(assemblyTitle);
    }
}
#pragma warning restore CS1591
