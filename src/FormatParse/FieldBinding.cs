using System.Linq.Expressions;
using System.Reflection;

namespace FormatParse;

internal interface IFieldBinding
{
    internal MemberInfo Member { get; }
    internal Type ValueType { get; }
    internal bool UsesDefaultParser { get; }
    internal Expression CreateConversion(Expression input, Expression provider, ParameterExpression result);
}

internal sealed class FieldBinding<T, TValue> : IFieldBinding
{
    private readonly string? _format;
    private readonly IValueParser<TValue>? _parser;

    internal FieldBinding(Expression<Func<T, TValue>> selector, string? format = null,
        IValueParser<TValue>? parser = null)
    {
        Member = GetMember(selector);
        ValueType = typeof(TValue);
        if (format is not null)
        {
            ExactValueConversion.Validate(ValueType, format);
        }

        _format = format;
        _parser = parser;
    }

    private static MemberInfo GetMember(Expression<Func<T, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        Expression body = selector.Body;

        // identity conversions are harmless; boxing and numeric conversions are not selectors.
        // i.e. int -> int ALLOWED, int -> long NOT ALLOWED
        while (body is UnaryExpression { NodeType: ExpressionType.Convert, Method: null } conversion &&
               conversion.Type == conversion.Operand.Type)
        {
            body = conversion.Operand;
        }

        // 1. must be a MemberExpression (i.e. x => x.Age, NOT ALLOW x => x.Age + 1)
        // 2. must be a direct member (i.e. NOT ALLOW x => x.Address.City)
        // 3. the member type must be TValue type
        if (body is not MemberExpression member || member.Expression != selector.Parameters[0] ||
            member.Type != typeof(TValue))
        {
            throw new ArgumentException(
                "Select a direct public instance property or field without conversions, calls, or nested access.",
                nameof(selector));
        }

        return member.Member switch
        {
            // if the member is a property, it must be a public non-static getter, and is not a indexer 
            PropertyInfo
            {
                GetMethod:
                {
                    IsPublic: true,
                    IsStatic: false,
                }
            } property when property.GetIndexParameters().Length == 0 => property,
            // or be a public non-static field
            FieldInfo
            {
                IsPublic: true,
                IsStatic: false,
            } field => field,
            _ => throw new ArgumentException(
                "Select a direct public instance property or field without conversions, calls, or nested access.",
                nameof(selector)),
        };
    }

    public MemberInfo Member { get; }
    public Type ValueType { get; }
    public bool UsesDefaultParser => _format is null && _parser is null;

    public Expression CreateConversion(Expression input, Expression provider, ParameterExpression result)
    {
        if (_parser is not null)
        {
            // The generated call preserves TValue, including the typed out argument.
            return Expression.Call(Expression.Constant(_parser, typeof(IValueParser<TValue>)),
                typeof(IValueParser<TValue>).GetMethod(nameof(IValueParser<TValue>.TryParse))!,
                input, provider, result);
        }

        return _format is null
            ? ValueConversion.Create(ValueType, input, provider, result)
            : ExactValueConversion.Create(ValueType, _format, input, provider, result);
    }
}
