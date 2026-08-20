using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace triaxis.EntityFrameworkCore.CompileModels.Tests;

/// <summary>
/// Reads what a built assembly declares. The point of the package is that the model ends up in the
/// consumer's own assembly, so nothing short of looking inside it answers whether it worked.
/// </summary>
sealed class CompiledAssembly : IDisposable
{
    readonly FileStream _file;
    readonly PEReader _pe;
    readonly MetadataReader _metadata;

    public CompiledAssembly(string path)
    {
        _file = File.OpenRead(path);
        _pe = new PEReader(_file);
        _metadata = _pe.GetMetadataReader();
    }

    public IEnumerable<string> TypeNames => _metadata.TypeDefinitions
        .Select(handle => _metadata.GetTypeDefinition(handle))
        .Select(type => $"{_metadata.GetString(type.Namespace)}.{_metadata.GetString(type.Name)}");

    public IEnumerable<string> AssemblyAttributes => _metadata.GetAssemblyDefinition()
        .GetCustomAttributes()
        .Select(handle => AttributeTypeName(_metadata.GetCustomAttribute(handle)));

    string AttributeTypeName(CustomAttribute attribute) => attribute.Constructor.Kind switch
    {
        HandleKind.MemberReference => TypeName(
            _metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent),
        HandleKind.MethodDefinition => TypeName(
            _metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
        _ => "",
    };

    string TypeName(EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeReference => _metadata.GetString(_metadata.GetTypeReference((TypeReferenceHandle)handle).Name),
        HandleKind.TypeDefinition => _metadata.GetString(_metadata.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
        _ => "",
    };

    public void Dispose()
    {
        _pe.Dispose();
        _file.Dispose();
    }
}
