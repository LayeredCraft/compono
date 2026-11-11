using System.Text;

namespace Compono.Logging;

/// <summary>
/// Computes the category name <c>LoggerFactory.CreateLogger&lt;T&gt;()</c> would use, using only
/// trim-safe <see cref="Type"/> name properties (no reflection over members, no
/// <c>MakeGenericType</c>). Mirrors the BCL's internal <c>TypeNameHelper.GetTypeDisplayName</c>
/// call with its logging parameters (<c>includeGenericParameters: false</c>,
/// <c>nestedTypeDelimiter: '.'</c>, <c>fullName: true</c>): a full namespace-qualified name, nested
/// types joined with <c>'.'</c>, generic arity and arguments removed, and C# keyword aliases for
/// built-in types. Internal - computed once per <see cref="CapturingLogger{T}"/> instance.
/// </summary>
internal static class LoggerCategoryName
{
    private static readonly Dictionary<Type, string> BuiltInTypeNames = new()
    {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(object)] = "object",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(string)] = "string",
        [typeof(uint)] = "uint",
        [typeof(ulong)] = "ulong",
        [typeof(ushort)] = "ushort",
    };

    public static string For(Type type)
    {
        var builder = new StringBuilder();
        AppendType(builder, type);
        return builder.ToString();
    }

    private static void AppendType(StringBuilder builder, Type type)
    {
        if (type.IsGenericType)
        {
            AppendGenericType(builder, type);
        }
        else if (type.IsArray)
        {
            AppendArrayType(builder, type);
        }
        else if (BuiltInTypeNames.TryGetValue(type, out var builtInName))
        {
            builder.Append(builtInName);
        }
        else
        {
            builder.Append(type.FullName!.Replace('+', '.'));
        }
    }

    private static void AppendArrayType(StringBuilder builder, Type type)
    {
        var elementType = type;
        while (elementType.IsArray)
            elementType = elementType.GetElementType()!;

        AppendType(builder, elementType);

        while (type.IsArray)
        {
            builder.Append('[');
            builder.Append(',', type.GetArrayRank() - 1);
            builder.Append(']');
            type = type.GetElementType()!;
        }
    }

    private static void AppendGenericType(StringBuilder builder, Type type)
    {
        if (type.IsNested)
        {
            AppendType(builder, type.DeclaringType!);
            builder.Append('.');
        }
        else if (!string.IsNullOrEmpty(type.Namespace))
        {
            builder.Append(type.Namespace);
            builder.Append('.');
        }

        var name = type.Name;
        var genericPartIndex = name.IndexOf('`');
        builder.Append(genericPartIndex <= 0 ? name : name[..genericPartIndex]);
    }
}
