using System.IO;
using System.Linq;

namespace EvaluacionesApp.Tests;

public sealed class AndroidResourcePackagingTests
{
    [Fact]
    public void Shared_getting_started_view_uses_assembly_relative_logo_resource()
    {
        var repo = FindRepositoryRoot();
        var viewPath = Path.Combine(
            repo,
            "EvaluacionesApp.Desktop",
            "Features",
            "GettingStarted",
            "Views",
            "GettingStartedView.axaml");

        var axaml = File.ReadAllText(viewPath);

        Assert.DoesNotContain("avares://EvaluacionesApp.Desktop/Assets/logo.png", axaml);
        Assert.Contains("Source=\"/Assets/logo.png\"", axaml);
    }

    [Fact]
    public void Android_project_packages_the_shared_logo_resource()
    {
        var repo = FindRepositoryRoot();
        var projectPath = Path.Combine(repo, "EvaluacionesApp.Android", "EvaluacionesApp.Android.csproj");

        var project = File.ReadAllText(projectPath);

        Assert.Contains("..\\assets\\logo.png", project);
        Assert.Contains("Link=\"Assets\\logo.png\"", project);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.EnumerateFiles("EvaluacionesApp.sln").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find EvaluacionesApp.sln from the test output directory.");
    }
}
