using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace FormatParse;

internal static class ExactValueConversion
{
    internal static void Validate(Type type, string format)
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        type = Nullable.GetUnderlyingType(type) ?? type;
        IFormattable sample;
        if (type == typeof(DateTime))
        {
            sample = new DateTime(2001, 2, 3, 4, 5, 6);
        }
        else if (type == typeof(DateTimeOffset))
        {
            sample = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);
        }
        else if (type == typeof(DateOnly))
        {
            sample = new DateOnly(2001, 2, 3);
        }
        else if (type == typeof(TimeOnly))
        {
            sample = new TimeOnly(4, 5, 6);
        }
        else if (type == typeof(TimeSpan))
        {
            sample = new TimeSpan(4, 5, 6);
        }
        else if (type == typeof(Guid))
        {
            sample = Guid.Empty;
        }
        else
        {
            throw new ArgumentException($"Field type '{type}' has no supported BCL exact-format parser.", nameof(format));
        }

        try
        {
            // Validate syntax with the BCL at configuration time, not on every input.
            // This is not a requirement that formatting round-trips every possible value.
            _ = sample.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException($"The format is not valid for '{type}'.", nameof(format), exception);
        }
    }

    internal static Expression Create(Type type, string format, Expression input, Expression provider,
        ParameterExpression result)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            ParameterExpression value = Expression.Variable(underlying, "nullableValue");
            Expression converted = Create(underlying, format, input, provider, value);
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

        // Reflection is build-time only. The compiled plan calls the typed span API directly.
        Expression formatSpan = Expression.Call(typeof(MemoryExtensions), nameof(MemoryExtensions.AsSpan),
            Type.EmptyTypes, Expression.Constant(format));
        List<Expression> arguments = [input, formatSpan];
        List<Type> parameterTypes = [typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>)];
        if (type != typeof(Guid))
        {
            arguments.Add(provider);
            parameterTypes.Add(typeof(IFormatProvider));
            if (type != typeof(TimeSpan))
            {
                arguments.Add(Expression.Constant(DateTimeStyles.None));
                parameterTypes.Add(typeof(DateTimeStyles));
            }
        }

        arguments.Add(result);
        parameterTypes.Add(type.MakeByRefType());
        MethodInfo method = type.GetMethod("TryParseExact", [.. parameterTypes])!;
        return Expression.Call(method, arguments);
    }
}