using System.Linq.Expressions;

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
    public void RootForksHaveIndependentBindingSequences()
    {
        FormatParseBuilder<User> root = Parser.For<User>("{}:{}");
        var forward = root.Fork().Bind(x => x.Name).Bind(x => x.Age);
        var reverse = root.Fork().Bind(x => x.Age).Bind(x => x.Name);
        Assert.Equal(new User("Alice", 18), forward.Compile().Parse("Alice:18"));
        Assert.Equal(new User("Bob", 24), reverse.Compile().Parse("24:Bob"));
        Assert.Throws<ArgumentException>(root.Compile);
        Assert.Equal(new User("Alice", 18), forward.Compile().Parse("Alice:18"));
    }

    [Fact]
    public void BindAppendsToTheSameConfigurationThroughEveryContext()
    {
        var root = Parser.For<User>("{}:{}");
        var name = root.Bind(x => x.Name);
        root.Bind(x => x.Age);

        Assert.Equal(new User("Alice", 18), name.Compile().Parse("Alice:18"));
        Assert.Equal(new User("Bob", 24), root.Compile().Parse("Bob:24"));
        Assert.Equal(new User("Cara", 30), name.Fork().Compile().Parse("Cara:30"));
        Assert.Throws<ArgumentException>(() => name.Bind(x => x.Name));
    }

    [Fact]
    public void FieldForksPreserveThePrefixAndDivergeIndependently()
    {
        var root = Parser.For<ScoredUser>("{}:{}:{}");
        FieldBindingBuilder<ScoredUser, string> common = root.Bind(x => x.Name);
        FieldBindingBuilder<ScoredUser, string> byAge = common.Fork();
        var byScore = common.Fork();

        var ageFirst = byAge.Bind(x => x.Age).Bind(x => x.Score).Compile();
        Assert.Equal(new ScoredUser("Alice", 18, 90), ageFirst.Parse("Alice:18:90"));
        Assert.Throws<ArgumentException>(byScore.Compile);
        Assert.Throws<ArgumentException>(common.Compile);
        Assert.Throws<ArgumentException>(root.Compile);

        var scoreFirst = byScore.Bind(x => x.Score).Bind(x => x.Age).Compile();
        common.Bind(x => x.Age).Bind(x => x.Score);
        Assert.Equal(new ScoredUser("Bob", 24, 80), scoreFirst.Parse("Bob:80:24"));
        Assert.Equal(new ScoredUser("Cara", 30, 70), root.Compile().Parse("Cara:30:70"));
        Assert.Equal(new ScoredUser("Alice", 18, 90), ageFirst.Parse("Alice:18:90"));
    }

    [Fact]
    public void FailedBindingsDoNotChangeTheConfiguration()
    {
        var root = Parser.For<User>("{}:{}");
        var name = root.Bind(x => x.Name);
        Assert.Throws<ArgumentException>(() => name.Bind(x => x.Name));
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Age + 1));
        Assert.Throws<ArgumentNullException>(() => root.Bind<int>(null!));
        Assert.Throws<ArgumentException>(() => root.Compile());

        name.Bind(x => x.Age);
        Assert.Equal(new User("Alice", 18), root.Compile().Parse("Alice:18"));
    }

    [Fact]
    public void CompiledParsersRemainStableAfterLaterConfigurationAttempts()
    {
        var root = Parser.For<User>("{}:{}");
        var complete = root.Bind(x => x.Name).Bind(x => x.Age);
        var parser = complete.Compile();

        // Compilation requires every capture, so no further append can be valid.
        Assert.Throws<ArgumentException>(() => root.Bind(x => x.Age));
        Assert.Throws<ArgumentException>(() => complete.Bind(x => x.Name));
        var fork = complete.Fork();
        Assert.Throws<ArgumentException>(() => fork.Bind(x => x.Age));
        Assert.Equal(new User("Alice", 18), fork.Compile().Parse("Alice:18"));
        Assert.Equal(new User("Bob", 24), parser.Parse("Bob:24"));
    }

    [Fact]
    public void BindingCountsAndDuplicateDestinationsAreConfigurationErrors()
    {
        var root = Parser.For<User>("{}:{}");
        Assert.Throws<ArgumentException>(root.Compile);
        var first = root.Bind(x => x.Name);
        Assert.Throws<ArgumentException>(first.Compile);
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
        Assert.Throws<ArgumentException>(() =>
            Parser.For<AmbiguousParameters>("{}:{}").Bind(x => x.Value).Bind(x => x.Other).Compile());
        Assert.Throws<ArgumentException>(() =>
            Parser.For<DuplicateParameter>("{}:{}").Bind(x => x.Value).Bind(x => x.VALUE).Compile());
    }

    private sealed record User(string Name, int Age)
    {
        public static int StaticValue => 42;
    }

    private sealed record ScoredUser(string Name, int Age, int Score);

    private sealed class NormalClass
    {
        public NormalClass(string name, int age)
        {
            Name = name;
            Age = age;
        }

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

    private sealed record MissingMember(int Value)
    {
        public int Other => Value;
    }

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
