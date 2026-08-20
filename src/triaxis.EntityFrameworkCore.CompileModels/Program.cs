using System.Collections;
using Microsoft.EntityFrameworkCore.Design;

// Asks EF Core to generate a compiled model. The work is EF's own; what this adds is a process to
// do it in. Microsoft.EntityFrameworkCore.Design derives the model by loading the project's
// assembly and everything it references, so it only works somewhere those resolve the way they do
// at run time. triaxis.EntityFrameworkCore.CompileModels.targets arranges exactly that: it starts
// this assembly with the built project's own deps.json and runtimeconfig.json, which is also where
// the Design assembly loaded below comes from -- the reference in the project file contributes
// compile-time types and nothing else.

var options = args.Chunk(2).ToDictionary(pair => pair[0], pair => pair[1]);

var executor = new OperationExecutor(
    new OperationReportHandler(
        Console.Error.WriteLine, Console.Error.WriteLine, Console.Out.WriteLine, _ => { }),
    new Hashtable
    {
        ["targetName"] = options["--assembly"],
        ["startupTargetName"] = options["--assembly"],
        ["projectDir"] = options["--project-dir"],
        ["rootNamespace"] = options["--root-namespace"],
        ["language"] = options["--language"],
        ["nullable"] = bool.Parse(options["--nullable"]),
        ["remainingArguments"] = Array.Empty<string>(),
    });

var result = new OperationResultHandler();
_ = new OperationExecutor.OptimizeContext(executor, result, new Hashtable
{
    ["outputDir"] = options["--output-dir"],
    ["modelNamespace"] = options["--namespace"],
    ["contextType"] = options["--context"],
    ["suffix"] = "",
    ["scaffoldModel"] = true,
    // Both belong to the NativeAOT flavour of the model, which loads slower than this one on
    // CoreCLR. Microsoft.EntityFrameworkCore.Tasks generates that flavour for projects that want it.
    ["precompileQueries"] = false,
    ["nativeAot"] = false,
});

if (result.ErrorType is null)
{
    return 0;
}

Console.Error.WriteLine($"{result.ErrorType}: {result.ErrorMessage}");
Console.Error.WriteLine(result.ErrorStackTrace);
return 1;
