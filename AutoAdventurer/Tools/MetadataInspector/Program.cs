using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: MetadataInspector <assembly> <name-pattern>");
    return 1;
}

using FileStream stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
MetadataReader reader = pe.GetMetadataReader();
string[] patterns = args[1].Split('|', StringSplitOptions.RemoveEmptyEntries);

foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
{
    TypeDefinition type = reader.GetTypeDefinition(handle);
    string ns = reader.GetString(type.Namespace);
    string name = reader.GetString(type.Name);
    string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    if (!patterns.Any(pattern => pattern.StartsWith('=')
            ? fullName.Equals(pattern[1..], StringComparison.OrdinalIgnoreCase)
            : fullName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        continue;

    string baseType = type.BaseType.IsNil ? "<none>" : type.BaseType.Kind.ToString();
    Console.WriteLine($"TYPE {fullName} BASE {baseType}");
    foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
        Console.WriteLine($"  FIELD {reader.GetString(reader.GetFieldDefinition(fieldHandle).Name)}");
    foreach (PropertyDefinitionHandle propertyHandle in type.GetProperties())
        Console.WriteLine($"  PROPERTY {reader.GetString(reader.GetPropertyDefinition(propertyHandle).Name)}");
    foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
    {
        MethodDefinition method = reader.GetMethodDefinition(methodHandle);
        string parameters = string.Join(", ", method.GetParameters()
            .Select(parameterHandle => reader.GetParameter(parameterHandle))
            .Where(parameter => parameter.SequenceNumber > 0)
            .OrderBy(parameter => parameter.SequenceNumber)
            .Select(parameter => reader.GetString(parameter.Name)));
        Console.WriteLine($"  METHOD {reader.GetString(method.Name)}({parameters})");
    }
}

return 0;
