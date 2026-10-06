using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace FormatParse;

internal static class ValueConversion
{
    internal static Expression Create(Type type, Expression input, Expression provider, ParameterExpression result)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying is null)
        {
            return Expression.Call(GetMethod(type), input, provider, result);
        }

        ParameterExpression value = Expression.Variable(underlying, "nullableValue");
        Expression converted = Create(underlying, input, provider, value);
        return Expression.Block([value],
            Expression.Assign(result, Expression.Default(type)),
            Expression.Condition(
                Expression.Property(input, nameof(ReadOnlySpan<char>.IsEmpty)),
                Expression.Constant(true),
                Expression.Condition(converted,
                    Expression.Block(Expression.Assign(result, Expression.Convert(value, type)),
                        Expression.Constant(true)),
                    Expression.Constant(false))));
    }

    private static MethodInfo GetMethod(Type type)
    {
        if (type == typeof(string))
        {
            return FindMethod(nameof(TryParseString));
        }

        if (type.IsEnum)
        {
            return FindMethod(nameof(TryParseEnum)).MakeGenericMethod(type);
        }

        if (type is { IsByRef: false, IsPointer: false, IsByRefLike: false })
        {
            Type[] interfaces = type.GetInterfaces();
            if (Implements(interfaces, typeof(ISpanParsable<>), type))
            {
                return FindMethod(nameof(TryParseSpan)).MakeGenericMethod(type);
            }

            if (Implements(interfaces, typeof(IParsable<>), type))
            {
                return FindMethod(nameof(TryParseValue)).MakeGenericMethod(type);
            }
        }

        throw new ArgumentException($"Field type '{type}' is not supported.", "pattern");
    }

    private static MethodInfo FindMethod(string name)
    {
        return typeof(ValueConversion).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static bool Implements(Type[] interfaces, Type definition, Type self)
    {
        foreach (Type contract in interfaces)
        {
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == definition &&
                contract.GenericTypeArguments[0] == self)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseString(ReadOnlySpan<char> input, IFormatProvider provider, out string result)
    {
        result = input.ToString();
        return true;
    }

    private static bool TryParseEnum<T>(ReadOnlySpan<char> input, IFormatProvider provider, out T result)
        where T : struct, Enum
    {
        return Enum.TryParse(input, ignoreCase: false, out result);
    }

    private static bool TryParseSpan<T>(ReadOnlySpan<char> input, IFormatProvider provider,
        [MaybeNullWhen(false)] out T result)
        where T : ISpanParsable<T>
    {
        return T.TryParse(input, provider, out result);
    }

    private static bool TryParseValue<T>(ReadOnlySpan<char> input, IFormatProvider provider,
        [MaybeNullWhen(false)] out T result)
        where T : IParsable<T>
    {
        // Only types lacking span parsing need a temporary string.
        return T.TryParse(input.ToString(), provider, out result);
    }
}
