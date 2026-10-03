using System.Linq.Expressions;
using System.Reflection;

namespace FormatParse;

internal abstract class FieldBinding
{
    protected FieldBinding(MemberInfo member, Type valueType)
    {
        Member = member;
        ValueType = valueType;
    }

    internal MemberInfo Member { get; }
    internal Type ValueType { get; }
}

internal sealed class FieldBinding<T, TValue> : FieldBinding
{
    internal FieldBinding(Expression<Func<T, TValue>> selector) : base(GetMember(selector), typeof(TValue))
    {
    }

    private static MemberInfo GetMember(Expression<Func<T, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        Expression body = selector.Body;

        // Identity conversions are harmless; boxing and numeric conversions are not selectors.
        while (body is UnaryExpression { NodeType: ExpressionType.Convert, Method: null } conversion &&
               conversion.Type == conversion.Operand.Type)
        {
            body = conversion.Operand;
        }

        if (body is MemberExpression member && member.Expression == selector.Parameters[0] && member.Type == typeof(TValue))
        {
            if (member.Member is PropertyInfo { GetMethod: { IsPublic: true, IsStatic: false } } property &&
                property.GetIndexParameters().Length == 0)
            {
                return property;
            }

            if (member.Member is FieldInfo { IsPublic: true, IsStatic: false } field)
            {
                return field;
            }
        }

        throw new ArgumentException("Select a direct public instance property or field without conversions, calls, or nested access.", nameof(selector));
    }
}
