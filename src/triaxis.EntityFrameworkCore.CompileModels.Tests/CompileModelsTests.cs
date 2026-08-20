namespace triaxis.EntityFrameworkCore.CompileModels.Tests;

public class CompileModelsTests
{
    [Test]
    public void ModelLandsInTheProjectsOwnAssembly()
    {
        using var fixture = FixtureProject.Create("", "BlogContext");
        fixture.Build();

        using var assembly = new CompiledAssembly(fixture.AssemblyPath);
        Assert.That(assembly.TypeNames, Does.Contain("Fixture.CompiledModels.BlogContextModel"));
        // What EF looks for to pick the model up without the context asking for it.
        Assert.That(assembly.AssemblyAttributes, Does.Contain("DbContextModelAttribute"));
    }

    [Test]
    public void NothingIsGeneratedWhenTurnedOff()
    {
        using var fixture = FixtureProject.Create("", "BlogContext");
        fixture.Build("-p:EFCompileModels=false");

        using var assembly = new CompiledAssembly(fixture.AssemblyPath);
        Assert.That(assembly.TypeNames, Has.None.StartsWith("Fixture.CompiledModels"));
    }

    [Test]
    public void EveryContextInTheProjectIsCompiled()
    {
        using var fixture = FixtureProject.Create("", "AlphaContext", "BetaContext");
        fixture.Build();

        using var assembly = new CompiledAssembly(fixture.AssemblyPath);
        Assert.That(assembly.TypeNames, Does.Contain("Fixture.CompiledModels.AlphaContextModel"));
        Assert.That(assembly.TypeNames, Does.Contain("Fixture.CompiledModels.BetaContextModel"));
    }

    [Test]
    public void NamespaceCanBeChosen()
    {
        using var fixture = FixtureProject.Create(
            "  <PropertyGroup><EFCompileModelsNamespace>Chosen.Model</EFCompileModelsNamespace></PropertyGroup>",
            "BlogContext");
        fixture.Build();

        using var assembly = new CompiledAssembly(fixture.AssemblyPath);
        Assert.That(assembly.TypeNames, Does.Contain("Chosen.Model.BlogContextModel"));
    }

    /// <summary>
    /// Generating costs a second compile, which is only worth paying when the first one happened.
    /// A build that changes nothing must not compile, and so must not generate either.
    /// </summary>
    [Test]
    public void UnchangedProjectIsNotBuiltAgain()
    {
        using var fixture = FixtureProject.Create("", "BlogContext");
        fixture.Build();
        var built = File.GetLastWriteTimeUtc(fixture.AssemblyPath);

        fixture.Build();

        Assert.That(File.GetLastWriteTimeUtc(fixture.AssemblyPath), Is.EqualTo(built));
    }
}
