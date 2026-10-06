using System.Linq.Expressions;

namespace FormatParse;

/// <summary>Provides the typed configuration context for a bound capture.</summary>
/// <typeparam name="T">The target type to construct.</typeparam>
/// <typeparam name="TValue">The current capture's value type.</typeparam>
/// <remarks>Shares its builder's mutable configuration. Use Fork for independent branches. This context is not thread-safe.</remarks>
public sealed class FieldBindingBuilder<T, TValue>
{
    private readonly FormatParseBuilder<T> _builder;

    internal FieldBindingBuilder(FormatParseBuilder<T> builder, FieldBinding<T, TValue> binding)
    {
        _builder = builder;
        Binding = binding;
    }

    // Retain the typed field node for future parsing and validation policies.
    internal FieldBinding<T, TValue> Binding { get; }

    /// <summary>Binds the next capture to a direct public member.</summary>
    /// <typeparam name="TNext">The next member's exact type.</typeparam>
    /// <param name="selector">A direct public property or field selector.</param>
    /// <returns>The typed context for the next capture, sharing the same configuration.</returns>
    /// <exception cref="ArgumentNullException">The selector is null.</exception>
    /// <exception cref="ArgumentException">The selector is unsupported, duplicated, or exceeds the capture count.</exception>
    public FieldBindingBuilder<T, TNext> Bind<TNext>(Expression<Func<T, TNext>> selector)
    {
        return _builder.Bind(selector);
    }

    /// <summary>Binds the next capture using a BCL exact parsing format.</summary>
    /// <typeparam name="TNext">The next member's exact type.</typeparam>
    /// <param name="selector">A direct public property or field selector.</param>
    /// <param name="format">A BCL exact format for a date, time, duration, or Guid.</param>
    /// <returns>The typed context sharing the same configuration.</returns>
    /// <exception cref="ArgumentNullException">The selector or format is null.</exception>
    /// <exception cref="ArgumentException">The selector, format, field type, or capture count is unsupported.</exception>
    public FieldBindingBuilder<T, TNext> Bind<TNext>(Expression<Func<T, TNext>> selector, string format)
    {
        return _builder.Bind(selector, format);
    }

    /// <summary>Binds the next capture using an explicit typed parser.</summary>
    /// <typeparam name="TNext">The next member's exact type.</typeparam>
    /// <param name="selector">A direct public property or field selector.</param>
    /// <param name="parser">The parser to retain and use for the next capture.</param>
    /// <returns>The typed context sharing the same configuration.</returns>
    /// <remarks>The parser instance is shared by forks and compiled plans. Its owner is responsible for its thread safety.</remarks>
    /// <exception cref="ArgumentNullException">The selector or parser is null.</exception>
    /// <exception cref="ArgumentException">The selector or capture count is unsupported.</exception>
    public FieldBindingBuilder<T, TNext> Bind<TNext>(Expression<Func<T, TNext>> selector, IValueParser<TNext> parser)
    {
        return _builder.Bind(selector, parser);
    }

    /// <summary>Copies the current configuration while preserving this capture's typed context.</summary>
    /// <returns>A typed context backed by an independent builder configuration.</returns>
    public FieldBindingBuilder<T, TValue> Fork()
    {
        return new FieldBindingBuilder<T, TValue>(_builder.Fork(), Binding);
    }

    /// <summary>Compiles all current bindings into an immutable parser.</summary>
    /// <returns>A parser that can be reused concurrently.</returns>
    /// <exception cref="ArgumentException">Bindings are incomplete or cannot map to the target constructor.</exception>
    public FormatParser<T> Compile()
    {
        return _builder.Compile();
    }
}