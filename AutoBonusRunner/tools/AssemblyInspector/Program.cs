using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: AssemblyInspector <assembly> <type-name-fragment>");
    return 2;
}

using FileStream stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
MetadataReader md = pe.GetMetadataReader();

foreach (TypeDefinitionHandle handle in md.TypeDefinitions)
{
    TypeDefinition type = md.GetTypeDefinition(handle);
    string ns = md.GetString(type.Namespace);
    string name = md.GetString(type.Name);
    string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    if (!fullName.Contains(args[1], StringComparison.OrdinalIgnoreCase))
        continue;

    Console.WriteLine($"TYPE {fullName}");
    foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
    {
        FieldDefinition field = md.GetFieldDefinition(fieldHandle);
        Console.WriteLine($"  FIELD {field.Attributes} {md.GetString(field.Name)} SIG={Convert.ToHexString(md.GetBlobBytes(field.Signature))}");
    }
    foreach (PropertyDefinitionHandle propertyHandle in type.GetProperties())
    {
        PropertyDefinition property = md.GetPropertyDefinition(propertyHandle);
        Console.WriteLine($"  PROP {md.GetString(property.Name)} SIG={Convert.ToHexString(md.GetBlobBytes(property.Signature))}");
    }
    foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
    {
        MethodDefinition method = md.GetMethodDefinition(methodHandle);
        Console.WriteLine(
            $"  METHOD {method.Attributes} {md.GetString(method.Name)} " +
            $"SIG={Convert.ToHexString(md.GetBlobBytes(method.Signature))}");
    }
}

return 0;
