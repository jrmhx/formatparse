using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace FormatParse;

internal delegate bool ObjectParser<T>(ReadOnlySpan<char> input, ReadOnlySpan<Range> captures, IFormatProvider provider,
    [MaybeNullWhen(false)] out T result);

internal static class TypeBinding<T>
{
    internal static ObjectParser<T> Create(int captureCount, IFieldBinding[]? bindings = null)
    {
        Type type = typeof(T);
        ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (type.IsAbstract || Nullable.GetUnderlyingType(type) is not null || constructors.Length != 1)
        {
            throw new ArgumentException(
                $"Target type '{type}' must be concrete and non-nullable, with exactly one public instance constructor.",
                "pattern");
        }

        ConstructorInfo constructor = constructors[0];
        ParameterInfo[] parameters = constructor.GetParameters();
        if (parameters.Length != captureCount)
        {
            throw new ArgumentException("The capture count must equal the constructor parameter count.", "pattern");
        }

        int[] captureIndices = GetCaptureIndices(parameters, bindings);
        ParameterExpression input = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
        ParameterExpression captures = Expression.Parameter(typeof(ReadOnlySpan<Range>), "captures");
        ParameterExpression provider = Expression.Parameter(typeof(IFormatProvider), "provider");
        ParameterExpression result = Expression.Parameter(typeof(T).MakeByRefType(), "result");
        ParameterExpression[] values = new ParameterExpression[parameters.Length];
        List<Expression> body = [Expression.Assign(result, Expression.Default(typeof(T)))];
        List<Expression> stringConversions = [];
        LabelTarget exit = Expression.Label(typeof(bool));
        MethodInfo captureMethod =
            typeof(TypeBinding<T>).GetMethod(nameof(GetCapture), BindingFlags.NonPublic | BindingFlags.Static)!;

        for (int index = 0; index < parameters.Length; index++)
        {
            Type valueType = parameters[index].ParameterType;
            if (valueType.IsByRef || valueType.IsPointer || valueType.IsByRefLike)
            {
                throw new ArgumentException($"Constructor parameter type '{valueType}' is not supported.", "pattern");
            }

            values[index] = Expression.Variable(valueType, $"value{index}");
            Expression capture =
                Expression.Call(captureMethod, input, captures, Expression.Constant(captureIndices[index]));
            Expression converted = ValueConversion.Create(valueType, capture, provider, values[index]);
            Expression step = Expression.IfThen(Expression.Not(converted),
                Expression.Return(exit, Expression.Constant(false)));
            if (valueType == typeof(string))
            {
                stringConversions.Add(step);
            }
            else
            {
                body.Add(step);
            }
        }

        // Delay string allocations until the other conversions succeed.
        body.AddRange(stringConversions);
        body.Add(Expression.Assign(result, Expression.New(constructor, values)));
        body.Add(Expression.Label(exit, Expression.Constant(true)));
        return Expression.Lambda<ObjectParser<T>>(
            Expression.Block(values, body), input, captures, provider, result).Compile();
    }

    private static int[] GetCaptureIndices(ParameterInfo[] parameters, IFieldBinding[]? bindings)
    {
        int[] indices = new int[parameters.Length];
        if (bindings is null)
        {
            for (int index = 0; index < indices.Length; index++)
            {
                indices[index] = index;
            }

            return indices;
        }

        if (bindings.Length != parameters.Length)
        {
            throw new ArgumentException("Bind each capture exactly once before compiling.", nameof(bindings));
        }

        Array.Fill(indices, -1);
        for (int captureIndex = 0; captureIndex < bindings.Length; captureIndex++)
        {
            IFieldBinding binding = bindings[captureIndex];
            int matchedIndex = -1;
            for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
            {
                ParameterInfo parameter = parameters[parameterIndex];
                if (!string.Equals(parameter.Name, binding.Member.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (matchedIndex >= 0)
                {
                    throw new ArgumentException(
                        $"Member '{binding.Member.Name}' matches multiple constructor parameters.", nameof(bindings));
                }

                matchedIndex = parameterIndex;
            }

            if (matchedIndex < 0 || parameters[matchedIndex].ParameterType != binding.ValueType)
            {
                throw new ArgumentException(
                    $"Member '{binding.Member.Name}' must match a constructor parameter by name and exact type.",
                    nameof(bindings));
            }

            if (indices[matchedIndex] >= 0)
            {
                throw new ArgumentException(
                    $"Constructor parameter '{parameters[matchedIndex].Name}' is bound more than once.",
                    nameof(bindings));
            }

            indices[matchedIndex] = captureIndex;
        }

        return indices;
    }

    private static ReadOnlySpan<char> GetCapture(ReadOnlySpan<char> input, ReadOnlySpan<Range> captures, int index)
    {
        return input[captures[index]];
    }
}
