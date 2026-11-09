using StardewAI.RuntimeTestHarness;

namespace StardewAI.Core.Tests;

public sealed class NativeTransientFieldCodecTests
{
    [Fact]
    public void DeclaredLayoutAndValueCastsPreserveOriginalFailurePolicy()
    {
        var source = new Fields();
        var text = NativeLiveTransientFieldCodec.RequireField(typeof(Fields), nameof(Fields.Text), typeof(string));
        var integer = NativeLiveTransientFieldCodec.RequireField(typeof(Fields), nameof(Fields.Integer), typeof(int));
        Assert.Equal("seed", NativeLiveTransientFieldCodec.ReadNullable<string>(text, source));
        Assert.Equal(-17, NativeLiveTransientFieldCodec.ReadValue<int>(integer, source));
        source.Text = null;
        Assert.Null(NativeLiveTransientFieldCodec.ReadNullable<string>(text, source));
        Assert.Throws<InvalidDataException>(() => NativeLiveTransientFieldCodec.RequireField(typeof(Fields), "absent", typeof(int)));
        Assert.Throws<InvalidDataException>(() => NativeLiveTransientFieldCodec.RequireField(typeof(Fields), nameof(Fields.Integer), typeof(bool)));
        Assert.Throws<InvalidDataException>(() => NativeLiveTransientFieldCodec.RequireField(typeof(Fields), nameof(Fields.Static), typeof(int)));
        Assert.Throws<InvalidDataException>(() => NativeLiveTransientFieldCodec.RequireField(typeof(Derived), nameof(Fields.Text), typeof(string)));
        Assert.Throws<InvalidDataException>(() => NativeLiveTransientFieldCodec.ReadValue<bool>(integer, source));
        Assert.Throws<InvalidDataException>(() => NativeLiveTransientFieldCodec.ReadNullable<string>(integer, source));
        Assert.Equal(-17, source.Integer);
    }

    [Fact]
    public void EmptyAndNullTextDoNotBorrowFromZeroBudget()
    {
        var zero = new NativeLiveTransientFieldCodec.CharacterBudget(0);
        Assert.Null(zero.String(null));
        Assert.Equal("", zero.String(""));
        Assert.Throws<InvalidDataException>(() => zero.String("x"));
        Assert.Equal(0, zero.Characters);
    }

    [Fact]
    public void TextIsBoundedAndLosslessWithoutChangingCountersAfterRejection()
    {
        var budget = new NativeLiveTransientFieldCodec.CharacterBudget(3);
        Assert.Equal("\ud83d\ude80", budget.String("\ud83d\ude80"));
        Assert.Equal(2, budget.Characters);
        Assert.Throws<InvalidDataException>(() => budget.String("xy"));
        Assert.Equal(2, budget.Characters);
        Assert.Equal("z", budget.String("z"));
        Assert.Equal(3, budget.Characters);
        Assert.Throws<InvalidDataException>(() => new NativeLiveTransientFieldCodec.CharacterBudget(65536).String(new string('x', 4097)));
        Assert.Throws<InvalidDataException>(() => new NativeLiveTransientFieldCodec.CharacterBudget(65536).String("\ud800"));
    }

    public class Fields { public string? Text = "seed"; public int Integer = -17; public static int Static = 1; }
    public sealed class Derived : Fields { }
}
