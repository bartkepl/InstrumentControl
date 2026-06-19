using InstrumentControl.Core.Blocks;
using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

public class MathBlockTests
{
    private static async Task<(bool Success, double Value, string? Error)> Eval(
        string expr, params (string Name, double Value)[] vars)
    {
        var b = new MathBlock();
        b.Deserialize(new BlockData
        {
            BlockId = "m",
            Properties = TestSupport.Props(("Expression", expr), ("ResultVariable", "out"))
        });
        var ctx = TestSupport.MakeContext();
        foreach (var (name, value) in vars) ctx.SetVariable(name, value);
        var r = await b.ExecuteAsync(ctx);
        return (r.Success, ctx.GetVariableAsDouble("out"), r.ErrorMessage);
    }

    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("2 + 3 * 4", 14)]            // precedence
    [InlineData("(2 + 3) * 4", 20)]          // grouping
    [InlineData("10 - 4 - 3", 3)]            // left assoc
    [InlineData("2 ^ 3 ^ 2", 512)]          // right assoc power
    [InlineData("-2 + 5", 3)]               // unary minus
    [InlineData("+4", 4)]                   // unary plus
    [InlineData("7 % 3", 1)]                // modulo
    [InlineData("10 / 4", 2.5)]
    public async Task Arithmetic(string expr, double expected)
    {
        var (success, value, _) = await Eval(expr);
        Assert.True(success);
        Assert.Equal(expected, value, 9);
    }

    [Theory]
    [InlineData("sqrt(16)", 4)]
    [InlineData("abs(-7)", 7)]
    [InlineData("pow(2, 10)", 1024)]
    [InlineData("min(3, 9)", 3)]
    [InlineData("max(3, 9)", 9)]
    [InlineData("clamp(15, 0, 10)", 10)]
    [InlineData("round(2.345, 2)", 2.35)]
    [InlineData("round(2.5)", 3)]
    [InlineData("floor(2.9)", 2)]
    [InlineData("ceil(2.1)", 3)]
    [InlineData("log(8, 2)", 3)]
    [InlineData("log10(1000)", 3)]
    [InlineData("log2(8)", 3)]
    [InlineData("hypot(3, 4)", 5)]
    [InlineData("sign(-5)", -1)]
    [InlineData("cbrt(27)", 3)]
    [InlineData("trunc(3.9)", 3)]
    public async Task Functions(string expr, double expected)
    {
        var (success, value, _) = await Eval(expr);
        Assert.True(success);
        Assert.Equal(expected, value, 9);
    }

    [Fact]
    public async Task Constants_Pi_E()
    {
        Assert.Equal(Math.PI, (await Eval("pi")).Value, 9);
        Assert.Equal(Math.E, (await Eval("e")).Value, 9);
    }

    [Fact]
    public async Task Trig_Deg2Rad()
    {
        var (success, value, _) = await Eval("sin(deg2rad(90))");
        Assert.True(success);
        Assert.Equal(1.0, value, 9);
    }

    [Fact]
    public async Task Variables_BracedAndBare()
    {
        Assert.Equal(7.0, (await Eval("{a} + {b}", ("a", 3), ("b", 4))).Value, 9);
        Assert.Equal(12.0, (await Eval("a * b", ("a", 3), ("b", 4))).Value, 9);
    }

    [Fact]
    public async Task ScientificNotation()
    {
        Assert.Equal(1500.0, (await Eval("1.5e3")).Value, 9);
    }

    [Fact]
    public async Task DivisionByZero_Fails()
    {
        var (success, _, error) = await Eval("1 / 0");
        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ModuloByZero_Fails()
    {
        Assert.False((await Eval("5 % 0")).Success);
    }

    [Fact]
    public async Task UnknownFunction_Fails()
    {
        Assert.False((await Eval("bogus(1)")).Success);
    }

    [Fact]
    public async Task UnbalancedBrace_Fails()
    {
        Assert.False((await Eval("{a + 1")).Success);
    }

    [Fact]
    public async Task UnknownCharacter_Fails()
    {
        Assert.False((await Eval("2 $ 3")).Success);
    }

    [Fact]
    public async Task MissingClosingParen_Fails()
    {
        Assert.False((await Eval("(2 + 3")).Success);
    }

    [Fact]
    public async Task FunctionMissingArgument_Fails()
    {
        Assert.False((await Eval("pow(2)")).Success);
    }

    [Fact]
    public async Task EmptyResultVariable_Fails()
    {
        var b = new MathBlock();
        b.Deserialize(new BlockData
        {
            BlockId = "m",
            Properties = TestSupport.Props(("Expression", "1 + 1"), ("ResultVariable", ""))
        });
        var r = await b.ExecuteAsync(TestSupport.MakeContext());
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Success_StoresResultAndContinues()
    {
        var b = new MathBlock();
        b.Deserialize(new BlockData
        {
            BlockId = "m",
            NextBlockId = "next",
            Properties = TestSupport.Props(("Expression", "{x} * 2"), ("ResultVariable", "y"))
        });
        var ctx = TestSupport.MakeContext();
        ctx.SetVariable("x", 21.0);
        var r = await b.ExecuteAsync(ctx);
        Assert.True(r.Success);
        Assert.Equal("next", r.NextBlockId);
        Assert.Equal(42.0, ctx.GetVariableAsDouble("y"), 9);
    }
}
