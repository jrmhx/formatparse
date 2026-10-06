using System.Linq.Expressions;
using System.Reflection;

namespace FormatParse;

internal interface IFieldBinding
{
    internal MemberInfo Member { get; }
    internal Type ValueType { get; }
}

internal sealed class FieldBinding<T, TValue> : IFieldBinding
{
    internal FieldBinding(Expression<Func<T, TValue>> selector)
    {
        Member = GetMember(selector);
        ValueType = typeof(TValue);
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
}
