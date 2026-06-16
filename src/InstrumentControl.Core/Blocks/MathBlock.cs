using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;
using InstrumentControl.Core.Base;
using InstrumentControl.Core.Models;
using InstrumentControl.Core.Services;

namespace InstrumentControl.Core.Blocks;

public class MathBlock : SequenceBlockBase
{
    public override string BlockType => "MathBlock";
    public override string DisplayName => "Matematyka";
    public override string Description =>
        "Oblicza wyrażenie matematyczne i zapisuje wynik do zmiennej. " +
        "Zmienne: {nazwa}. Funkcje: sqrt, abs, pow, sin, cos, tan, log, round, min, max, ...";
    public override Color BlockColor => Color.FromRgb(0x1A, 0xBC, 0x9C);
    public override string Category => "Dane";

    public override IEnumerable<BlockPropertyDefinition> PropertyDefinitions =>
    [
        BlockPropertyDefinition.Text("Expression", "Wyrażenie", "{a} + {b}"),
        BlockPropertyDefinition.Variable("ResultVariable", "Wynik → zmienna", "wynik"),
    ];

    public override Task<BlockExecutionResult> ExecuteAsync(SequenceContext context)
    {
        string expr = GetPropStr("Expression", "0");
        string resultVar = GetPropStr("ResultVariable", "");

        if (string.IsNullOrEmpty(resultVar))
            return Task.FromResult(BlockExecutionResult.Fail("Nie podano nazwy zmiennej wynikowej"));

        try
        {
            // Replace {varName} occurrences with their numeric values before parsing
            string preprocessed = Regex.Replace(expr, @"\{(\w+)\}", m =>
                context.GetVariableAsDouble(m.Groups[1].Value, 0)
                    .ToString(CultureInfo.InvariantCulture));

            double result = MathExprEvaluator.Evaluate(preprocessed,
                name => context.GetVariableAsDouble(name, 0));

            context.SetVariable(resultVar, result);
            context.Log?.Invoke($"Math: {expr} = {result:G6} → {resultVar}");
            return Task.FromResult(BlockExecutionResult.Ok(NextBlockId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BlockExecutionResult.Fail($"Błąd wyrażenia '{expr}': {ex.Message}"));
        }
    }

    static MathBlock() => BlockRegistry.Register("MathBlock", () => new MathBlock());
}

// ── Expression evaluator ─────────────────────────────────────────────────────
// Recursive descent parser: supports +  -  *  /  %  ^ (power), unary minus,
// parentheses, constants (pi, e), and Math functions (sqrt, abs, sin, cos, ...).
// Bare identifiers are looked up as variables via the provided delegate.

internal static class MathExprEvaluator
{
    private enum TKind { Num, Plus, Minus, Star, Slash, Pct, Caret, LP, RP, Comma, Ident, Eof }
    private readonly record struct Token(TKind Kind, string Raw, double Num = 0);

    public static double Evaluate(string expression, Func<string, double> getVar)
    {
        var tokens = Tokenize(expression);
        int pos = 0;
        double result = ParseAddSub(tokens, ref pos, getVar);
        if (tokens[pos].Kind != TKind.Eof)
            throw new InvalidOperationException($"Nieoczekiwany token: '{tokens[pos].Raw}'");
        return result;
    }

    // ── Tokenizer ─────────────────────────────────────────────────────────────

    private static List<Token> Tokenize(string s)
    {
        var list = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }

            if (char.IsDigit(s[i]) || s[i] == '.')
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                    || ((s[i] == '+' || s[i] == '-') && i > start && (s[i - 1] == 'e' || s[i - 1] == 'E'))))
                    i++;
                string raw = s[start..i];
                list.Add(new Token(TKind.Num, raw, double.Parse(raw, CultureInfo.InvariantCulture)));
                continue;
            }

            if (char.IsLetter(s[i]) || s[i] == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                list.Add(new Token(TKind.Ident, s[start..i]));
                continue;
            }

            list.Add(s[i] switch
            {
                '+' => new Token(TKind.Plus,  "+"),
                '-' => new Token(TKind.Minus, "-"),
                '*' => new Token(TKind.Star,  "*"),
                '/' => new Token(TKind.Slash, "/"),
                '%' => new Token(TKind.Pct,   "%"),
                '^' => new Token(TKind.Caret, "^"),
                '(' => new Token(TKind.LP,    "("),
                ')' => new Token(TKind.RP,    ")"),
                ',' => new Token(TKind.Comma, ","),
                _ => throw new InvalidOperationException($"Nieznany znak: '{s[i]}'")
            });
            i++;
        }
        list.Add(new Token(TKind.Eof, ""));
        return list;
    }

    // ── Recursive descent ─────────────────────────────────────────────────────

    // expr   = term  { ('+' | '-') term }
    // term   = power { ('*' | '/' | '%') power }
    // power  = unary [ '^' power ]        (right-associative)
    // unary  = ['-' | '+'] primary
    // primary= NUMBER | '(' expr ')' | IDENT [ '(' arglist ')' ]

    private static double ParseAddSub(List<Token> t, ref int p, Func<string, double> gv)
    {
        double v = ParseMulDiv(t, ref p, gv);
        while (t[p].Kind is TKind.Plus or TKind.Minus)
        {
            var op = t[p++].Kind;
            double r = ParseMulDiv(t, ref p, gv);
            v = op == TKind.Plus ? v + r : v - r;
        }
        return v;
    }

    private static double ParseMulDiv(List<Token> t, ref int p, Func<string, double> gv)
    {
        double v = ParsePower(t, ref p, gv);
        while (t[p].Kind is TKind.Star or TKind.Slash or TKind.Pct)
        {
            var op = t[p++].Kind;
            double r = ParsePower(t, ref p, gv);
            v = op switch
            {
                TKind.Star  => v * r,
                TKind.Slash => r == 0 ? throw new DivideByZeroException("Dzielenie przez zero") : v / r,
                _           => v % r
            };
        }
        return v;
    }

    private static double ParsePower(List<Token> t, ref int p, Func<string, double> gv)
    {
        double v = ParseUnary(t, ref p, gv);
        if (t[p].Kind == TKind.Caret) { p++; return Math.Pow(v, ParsePower(t, ref p, gv)); }
        return v;
    }

    private static double ParseUnary(List<Token> t, ref int p, Func<string, double> gv)
    {
        if (t[p].Kind == TKind.Minus) { p++; return -ParsePrimary(t, ref p, gv); }
        if (t[p].Kind == TKind.Plus)  { p++; return  ParsePrimary(t, ref p, gv); }
        return ParsePrimary(t, ref p, gv);
    }

    private static double ParsePrimary(List<Token> t, ref int p, Func<string, double> gv)
    {
        if (t[p].Kind == TKind.Num) return t[p++].Num;

        if (t[p].Kind == TKind.LP)
        {
            p++;
            double v = ParseAddSub(t, ref p, gv);
            if (t[p].Kind != TKind.RP) throw new InvalidOperationException("Oczekiwano ')'");
            p++;
            return v;
        }

        if (t[p].Kind == TKind.Ident)
        {
            string name = t[p++].Raw;

            if (t[p].Kind == TKind.LP)           // function call
            {
                p++;
                var args = new List<double>();
                if (t[p].Kind != TKind.RP)
                {
                    args.Add(ParseAddSub(t, ref p, gv));
                    while (t[p].Kind == TKind.Comma) { p++; args.Add(ParseAddSub(t, ref p, gv)); }
                }
                if (t[p].Kind != TKind.RP) throw new InvalidOperationException("Oczekiwano ')'");
                p++;
                return CallFunc(name.ToLowerInvariant(), args);
            }

            // constants
            if (name.Equals("pi",  StringComparison.OrdinalIgnoreCase)) return Math.PI;
            if (name.Equals("e",   StringComparison.OrdinalIgnoreCase)) return Math.E;
            if (name.Equals("inf", StringComparison.OrdinalIgnoreCase)) return double.PositiveInfinity;

            // variable lookup
            return gv(name);
        }

        throw new InvalidOperationException($"Nieoczekiwany token: '{t[p].Raw}'");
    }

    // ── Built-in functions ────────────────────────────────────────────────────

    private static double CallFunc(string name, List<double> a)
    {
        double A0() => a.Count > 0 ? a[0] : throw Arg(name, 0);
        double A1() => a.Count > 1 ? a[1] : throw Arg(name, 1);
        double A2() => a.Count > 2 ? a[2] : throw Arg(name, 2);

        return name switch
        {
            "sqrt"              => Math.Sqrt(A0()),
            "cbrt"              => Math.Cbrt(A0()),
            "abs"               => Math.Abs(A0()),
            "sign"              => Math.Sign(A0()),
            "sin"               => Math.Sin(A0()),
            "cos"               => Math.Cos(A0()),
            "tan"               => Math.Tan(A0()),
            "asin"              => Math.Asin(A0()),
            "acos"              => Math.Acos(A0()),
            "atan"              => Math.Atan(A0()),
            "atan2"             => Math.Atan2(A0(), A1()),
            "sinh"              => Math.Sinh(A0()),
            "cosh"              => Math.Cosh(A0()),
            "tanh"              => Math.Tanh(A0()),
            "exp"               => Math.Exp(A0()),
            "log"  when a.Count == 2 => Math.Log(A0(), A1()),
            "log"               => Math.Log(A0()),
            "log10"             => Math.Log10(A0()),
            "log2"              => Math.Log2(A0()),
            "pow"               => Math.Pow(A0(), A1()),
            "round" when a.Count == 2 => Math.Round(A0(), (int)A1(), MidpointRounding.AwayFromZero),
            "round"             => Math.Round(A0(), MidpointRounding.AwayFromZero),
            "floor"             => Math.Floor(A0()),
            "ceil" or "ceiling" => Math.Ceiling(A0()),
            "trunc"             => Math.Truncate(A0()),
            "min"               => Math.Min(A0(), A1()),
            "max"               => Math.Max(A0(), A1()),
            "clamp"             => Math.Clamp(A0(), A1(), A2()),
            "deg2rad"           => A0() * Math.PI / 180.0,
            "rad2deg"           => A0() * 180.0 / Math.PI,
            "hypot"             => Math.Sqrt(A0() * A0() + A1() * A1()),
            _                   => throw new InvalidOperationException($"Nieznana funkcja: '{name}'")
        };
    }

    private static InvalidOperationException Arg(string fn, int idx) =>
        new($"Funkcja '{fn}' wymaga argumentu {idx + 1}");
}
