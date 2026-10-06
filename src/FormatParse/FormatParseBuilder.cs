using System.Linq.Expressions;

namespace FormatParse;

/// <summary>Builds a parser with explicitly bound captures.</summary>
/// <typeparam name="T">The target type to construct.</typeparam>
/// <remarks>Bindings are appended to this configuration. Use Fork to create an independent branch. Builders are not thread-safe.</remarks>
public sealed class FormatParseBuilder<T>
{
    private readonly Pattern _pattern;

    private readonly List<IFieldBinding> _bindings;

    internal FormatParseBuilder(Pattern pattern)
    {
        _pattern = pattern;
        _bindings = new List<IFieldBinding>(pattern.CaptureCount);
    }

    private FormatParseBuilder(FormatParseBuilder<T> source) : this(source._pattern)
    {
        // Field configuration is immutable; custom parser instances are shared.
        _bindings.AddRange(source._bindings);
    }

    /// <summary>Binds the next capture to a direct public member.</summary>
    /// <typeparam name="TValue">The selected member's exact type.</typeparam>
    /// <param name="selector">A direct property or field selector, such as x => x.Age.</param>
    /// <returns>The typed context for this capture, sharing this builder's configuration.</returns>
    /// <exception cref="ArgumentNullException">The selector is null.</exception>
    /// <exception cref="ArgumentException">The selector is unsupported, duplicated, or exceeds the capture count.</exception>
    public FieldBindingBuilder<T, TValue> Bind<TValue>(Expression<Func<T, TValue>> selector)
    {
        return Append(new FieldBinding<T, TValue>(selector));
    }

    /// <summary>Binds the next capture using a BCL exact parsing format.</summary>
    /// <typeparam name="TValue">The selected member's exact type.</typeparam>
    /// <param name="selector">A direct public property or field selector.</param>
    /// <param name="format">A BCL exact format for a date, time, duration, or Guid.</param>
    /// <returns>The typed context sharing this builder's configuration.</returns>
    /// <exception cref="ArgumentNullException">The selector or format is null.</exception>
    /// <exception cref="ArgumentException">The selector, format, field type, or capture count is unsupported.</exception>
    public FieldBindingBuilder<T, TValue> Bind<TValue>(Expression<Func<T, TValue>> selector, string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Append(new FieldBinding<T, TValue>(selector, format: format));
    }

    /// <summary>Binds the next capture using an explicit typed parser.</summary>
    /// <typeparam name="TValue">The selected member's exact type.</typeparam>
    /// <param name="selector">A direct public property or field selector.</param>
    /// <param name="parser">The parser to retain and use for this capture.</param>
    /// <returns>The typed context sharing this builder's configuration.</returns>
    /// <remarks>The parser instance is shared by forks and compiled plans. Its owner is responsible for its thread safety.</remarks>
    /// <exception cref="ArgumentNullException">The selector or parser is null.</exception>
    /// <exception cref="ArgumentException">The selector or capture count is unsupported.</exception>
    public FieldBindingBuilder<T, TValue> Bind<TValue>(Expression<Func<T, TValue>> selector, IValueParser<TValue> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        return Append(new FieldBinding<T, TValue>(selector, parser: parser));
    }

    private FieldBindingBuilder<T, TValue> Append<TValue>(FieldBinding<T, TValue> binding)
    {
        if (_bindings.Count >= _pattern.CaptureCount)
        {
            throw new ArgumentException("All captures have already been bound.", "selector");
        }

        foreach (IFieldBinding existing in _bindings)
        {
            if (existing.Member.Equals(binding.Member))
            {
                throw new ArgumentException("A member cannot be bound more than once.", "selector");
            }
        }

        _bindings.Add(binding);
        return new FieldBindingBuilder<T, TValue>(this, binding);
    }

    /// <summary>Copies the current configuration into an independent builder.</summary>
    /// <returns>A builder with the same bindings and its own binding collection.</returns>
    public FormatParseBuilder<T> Fork()
    {
        return new FormatParseBuilder<T>(this);
    }

    /// <summary>Compiles the current bindings into an immutable parser.</summary>
    /// <returns>A parser that can be reused concurrently.</returns>
    /// <exception cref="ArgumentException">Bindings are incomplete or cannot map to the target constructor.</exception>
    public FormatParser<T> Compile()
    {
        if (_bindings.Count != _pattern.CaptureCount)
        {
            throw new ArgumentException("Bind each capture exactly once before compiling.");
        }

        // The runtime plan is compiled from a snapshot and never retains this list.
        return new FormatParser<T>(_pattern, TypeBinding<T>.Create(_pattern.CaptureCount, [.. _bindings]));
    }
}