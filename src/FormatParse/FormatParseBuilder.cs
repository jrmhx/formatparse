using System.Linq.Expressions;

namespace FormatParse;

/// <summary>Builds a parser with explicitly bound captures.</summary>
/// <typeparam name="T">The target type to construct.</typeparam>
/// <remarks>Each Bind call returns an independent configuration. Builders are immutable.</remarks>
public sealed class FormatParseBuilder<T>
{
    private readonly Pattern _pattern;
    private readonly FieldBinding[] _bindings;

    internal FormatParseBuilder(Pattern pattern, FieldBinding[]? bindings = null)
    {
        _pattern = pattern;
        _bindings = bindings ?? [];
    }

    /// <summary>Binds the next capture to a direct public member.</summary>
    /// <typeparam name="TValue">The selected member's exact type.</typeparam>
    /// <param name="selector">A direct property or field selector, such as x => x.Age.</param>
    /// <returns>The typed configuration context for this capture.</returns>
    /// <exception cref="ArgumentNullException">The selector is null.</exception>
    /// <exception cref="ArgumentException">The selector is unsupported, duplicated, or exceeds the capture count.</exception>
    public FieldBindingBuilder<T, TValue> Bind<TValue>(Expression<Func<T, TValue>> selector)
    {
        FieldBinding<T, TValue> binding = new(selector);
        if (_bindings.Length >= _pattern.CaptureCount)
        {
            throw new ArgumentException("All captures have already been bound.", nameof(selector));
        }

        foreach (FieldBinding existing in _bindings)
        {
            if (existing.Member.Equals(binding.Member))
            {
                throw new ArgumentException("A member cannot be bound more than once.", nameof(selector));
            }
        }

        FieldBinding[] bindings = new FieldBinding[_bindings.Length + 1];
        _bindings.CopyTo(bindings, 0);
        bindings[^1] = binding;
        return new FieldBindingBuilder<T, TValue>(new FormatParseBuilder<T>(_pattern, bindings), binding);
    }

    /// <summary>Compiles the current bindings into an immutable parser.</summary>
    /// <returns>A parser that can be reused concurrently.</returns>
    /// <exception cref="ArgumentException">Bindings are incomplete or cannot map to the target constructor.</exception>
    public FormatParser<T> Compile()
    {
        if (_bindings.Length != _pattern.CaptureCount)
        {
            throw new ArgumentException("Bind each capture exactly once before compiling.");
        }

        return new FormatParser<T>(_pattern, TypeBinding<T>.Create(_pattern.CaptureCount, _bindings));
    }
}
