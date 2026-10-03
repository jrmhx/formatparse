using System.Linq.Expressions;

using FormatParse;

namespace FormatParse.Tests;

public sealed class BindingTests
{
    [Fact]
    public void TypedContextsSupportReorderedConstructorBinding()
    {
        FieldBindingBuilder<User, int> age = Parser.For<User>("Age={}; Name={}").Bind(x => x.Age);
        FieldBindingBuilder<User, string> name = age.Bind(x => x.Name);
        FormatParser<User> parser = name.Compile();
        Assert.Equal(new User("Alice", 18), parser.Parse("Age=18; Name=Alice"));
        Assert.False(parser.TryParse("Age=bad; Name=Alice", out _));
        Assert.Equal(new User("Bob", 24), parser.Parse("Age=24; Name=Bob"));
        Parallel.For(0, 100, index =>
        {
            Assert.Equal(new User("User", index), parser.Parse($"Age={index}; Name=User"));
        });
    }

    [Fact]
    public void PropertiesAndFieldsMapByNameIgnoringCaseWithExactTypes()
    {
        var propertyParser = Parser.For<NormalClass>("{}:{}").Bind(x => x.Age).Bind(x => x.Name).Compile();
        NormalClass value = propertyParser.Parse("18:Alice");
        Assert.Equal("Alice", value.Name);
        Assert.Equal(18, value.Age);

        var fieldParser = Parser.For<FieldTarget>("{}").Bind(x => x.Value).Compile();
        Assert.Equal(42, fieldParser.Parse("42").Value);
        Assert.NotNull(Parser.For<Empty>("ready").Compile().Parse("ready"));
        Assert.Equal(42, Parser.For<InheritedTarget>("{}").Bind(x => x.Value).Compile().Parse("42").Value);
    }

    [Fact]
    public void BuildersAreIndependentSnapshots()
    {
        FormatParseBuilder<User> root = Parser.For<User>("{}:{}");
        var forward = root.Bind(x => x.Name).Bind(x => x.Age);
        var reverse = root.Bind(x => x.Age).Bind(x => x.Name);
        Assert.Equal(new User("Alice", 18), forward.Compile().Parse("Alice:18"));
        Assert.Equal(new User("Bob", 24), reverse.Compile().Parse("24:Bob"));
        Assert.Throws<ArgumentException>(() => root.Compile());
        Assert.Equal(new User("Alice", 18), forward.Compile().Parse("Alice:18"));
    }

    [Fact]
    public void BindingCountsAndDuplicateDestinationsAreConfigurationErrors()
    {
        var root = Parser.For<User>("{}:{}");
        Assert.Throws<ArgumentException>(() => root.Compile());
        var first = root.Bind(x => x.Name);
        Assert.Throws<ArgumentException>(() => first.Compile());
        Assert.Throws<ArgumentException>(() => first.Bind(x => x.Name));
        Assert.Throws<ArgumentException>(() => first.Bind(x => x.Age).Bind(x => x.Age));
        Assert.Throws<ArgumentException>(() => Parser.For<User>("{}").Bind(x => x.Name).Compile());
    }

    [Fact]
    public void ArbitrarySelectorsAndTypeErasureAreRejected()
    {
        var root = Parser.For<User>("{}:{}");
        Assert.Throws<ArgumentNullException>(() => root.Bind<string>(null!));
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Age + 1));
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Name.ToUpperInvariant()));
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Name.Length));
        Assert.Throws<ArgumentException>(() => root.Bind(x => User.StaticValue));
        Assert.Throws<ArgumentException>(() => root.Bind(x => 42));
        Assert.Throws<ArgumentException>(() => root.Bind(x => (long)x.Age));
        Assert.Throws<ArgumentException>(() => root.Bind<object>(x => x.Age));
        Assert.Throws<ArgumentException>(() => Parser.For<IndexerTarget>("{}").Bind(x => x[0]));
        ParameterExpression privateTarget = Expression.Parameter(typeof(PrivateGetter), "x");
        var privateSelector = Expression.Lambda<Func<PrivateGetter, int>>(
            Expression.Property(privateTarget, nameof(PrivateGetter.Value)), privateTarget);
        Assert.Throws<ArgumentException>(() => Parser.For<PrivateGetter>("{}").Bind(privateSelector));

        // A manually constructed identity conversion preserves the member's type.
        ParameterExpression parameter = Expression.Parameter(typeof(FieldTarget), "x");
        var selector = Expression.Lambda<Func<FieldTarget, int>>(
            Expression.Convert(Expression.Field(parameter, nameof(FieldTarget.Value)), typeof(int)), parameter);
        Assert.Equal(42, Parser.For<FieldTarget>("{}").Bind(selector).Compile().Parse("42").Value);
    }

    [Fact]
    public void MemberMappingRejectsMissingMismatchedAndAmbiguousParameters()
    {
        Assert.Throws<ArgumentException>(() => Parser.For<MissingMember>("{}").Bind(x => x.Other).Compile());
        Assert.Throws<ArgumentException>(() => Parser.For<MismatchedMember>("{}").Bind(x => x.Value).Compile());
        Assert.Throws<ArgumentException>(() => Parser.For<AmbiguousParameters>("{}:{}").Bind(x => x.Value).Bind(x => x.Other).Compile());
        Assert.Throws<ArgumentException>(() => Parser.For<DuplicateParameter>("{}:{}").Bind(x => x.Value).Bind(x => x.VALUE).Compile());
    }

    private sealed record User(string Name, int Age)
    {
        public static int StaticValue => 42;
    }

    private sealed class NormalClass
    {
        public NormalClass(string name, int age) { Name = name; Age = age; }
        public string Name { get; }
        public int Age { get; }
    }

    private sealed class FieldTarget
    {
        public FieldTarget(int value) { Value = value; }
        public readonly int Value;
    }

    private class BaseTarget
    {
        public int Value { get; protected set; }
    }

    private sealed class InheritedTarget : BaseTarget
    {
        public InheritedTarget(int value) { Value = value; }
    }

    private sealed record Empty;
    private sealed record MissingMember(int Value) { public int Other => Value; }

    private sealed class MismatchedMember
    {
        public MismatchedMember(int value) { Value = value; }
        public long Value { get; }
    }

    private sealed class AmbiguousParameters
    {
        public AmbiguousParameters(int value, int VALUE) { }
        public int Value => 1;
        public int Other => 2;
    }

    private sealed class DuplicateParameter
    {
        public DuplicateParameter(int value, int other) { }
        public int Value => 1;
        public int VALUE => 2;
    }

    private sealed class IndexerTarget
    {
        public IndexerTarget(int value) { }
        public int this[int index] => index;
    }

    private sealed class PrivateGetter
    {
        public PrivateGetter(int value) { Value = value; }
        public int Value { private get; set; }
    }
}
