using System.Diagnostics;
using System.Reflection;

namespace triaxis.EntityFrameworkCore.CompileModels.Tests;

/// <summary>
/// A throwaway EF Core project, generated into a temporary directory and built against the packed
/// package, which is the only way to see what the package does to a build that is not its own.
/// </summary>
sealed class FixtureProject : IDisposable
{
    const string Name = "Fixture";
    const string TargetFramework = "net10.0";

    static readonly Dictionary<string, string> BuildMetadata =
        typeof(FixtureProject).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value ?? "");

    readonly string _directory;

    FixtureProject(string directory) => _directory = directory;

    public string AssemblyPath => Path.Combine(_directory, "bin", "Debug", TargetFramework, $"{Name}.dll");

    /// <param name="projectFragment">Extra MSBuild elements for the fixture's project file.</param>
    /// <param name="contexts">Names of the context classes to generate.</param>
    public static FixtureProject Create(string projectFragment, params string[] contexts)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"efcm-{Guid.NewGuid():n}");
        Directory.CreateDirectory(directory);
        var fixture = new FixtureProject(directory);

        var ef = BuildMetadata["EFCoreVersion"];
        fixture.Write($"{Name}.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{TargetFramework}</TargetFramework>
                <AssemblyName>{Name}</AssemblyName>
                <RootNamespace>{Name}</RootNamespace>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="{ef}" />
                <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="{ef}" PrivateAssets="all" />
                <PackageReference Include="{BuildMetadata["PackageUnderTest"]}" Version="{BuildMetadata["PackageUnderTestVersion"]}" PrivateAssets="all" />
              </ItemGroup>
            {projectFragment}
            </Project>
            """);

        fixture.Write("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                <add key="package-under-test" value="{BuildMetadata["PackageUnderTestFeed"]}" />
              </packageSources>
            </configuration>
            """);

        // The fixture must be built by the package alone, whatever the directory it lands in says.
        fixture.Write("Directory.Build.props", "<Project />");
        fixture.Write("Directory.Build.targets", "<Project />");

        fixture.Write("Blog.cs", $$"""
            namespace {{Name}};

            public class Blog
            {
                public int Id { get; set; }
                public string? Title { get; set; }
            }
            """);

        foreach (var context in contexts)
        {
            fixture.Write($"{context}.cs", $$"""
                using Microsoft.EntityFrameworkCore;

                namespace {{Name}};

                public class {{context}} : DbContext
                {
                    public DbSet<Blog> Blogs => Set<Blog>();

                    protected override void OnConfiguring(DbContextOptionsBuilder options)
                        => options.UseSqlite("Data Source=fixture.db");
                }
                """);
        }

        return fixture;
    }

    void Write(string name, string content) => File.WriteAllText(Path.Combine(_directory, name), content);

    public string Build(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("Debug");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        // Leaving build nodes running would hold on to the temporary directory after the test.
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var log = output.Result + errors.Result;
        Assert.That(process.ExitCode, Is.Zero, log);
        return log;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A directory that outlives the test is litter, not a failure.
        }
    }
}
