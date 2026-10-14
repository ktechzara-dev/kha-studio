using System.Globalization;

namespace VisionStudio.Core.Expressions;

/// <summary>
/// Fast, safe lightweight mathematical and logical expression evaluator for condition checking,
/// table filtering, and parameter thresholds without script sandbox overhead.
/// </summary>
public sealed class ExpressionEvaluator
{
    private readonly string _text;
    private int _pos;

    public ExpressionEvaluator(string expression)
    {
        _text = expression ?? string.Empty;
        _pos = 0;
    }

    public static object? Evaluate(string expression, IReadOnlyDictionary<string, object?> variables)
    {
        var eval = new ExpressionEvaluator(expression);
        return eval.ParseExpression(variables);
    }

    public static bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> variables, bool defaultValue = false)
    {
        try
        {
            var res = Evaluate(expression, variables);
            if (res is bool b) return b;
            if (res is double d) return d != 0;
            if (res != null && bool.TryParse(res.ToString(), out var parsed)) return parsed;
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public static double EvaluateDouble(string expression, IReadOnlyDictionary<string, object?> variables, double defaultValue = 0.0)
    {
        try
        {
            var res = Evaluate(expression, variables);
            if (res is double d) return d;
            if (res is float f) return f;
            if (res is int i) return i;
            if (res is bool b) return b ? 1.0 : 0.0;
            if (res != null && double.TryParse(res.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private object? ParseExpression(IReadOnlyDictionary<string, object?> vars)
    {
        return ParseLogicalOr(vars);
    }

    private object? ParseLogicalOr(IReadOnlyDictionary<string, object?> vars)
    {
        var left = ParseLogicalAnd(vars);
        SkipWhitespace();

        while (Match("||"))
        {
            var right = ParseLogicalAnd(vars);
            left = ToBool(left) || ToBool(right);
            SkipWhitespace();
        }

        return left;
    }

    private object? ParseLogicalAnd(IReadOnlyDictionary<string, object?> vars)
    {
        var left = ParseEquality(vars);
        SkipWhitespace();

        while (Match("&&"))
        {
            var right = ParseEquality(vars);
            left = ToBool(left) && ToBool(right);
            SkipWhitespace();
        }

        return left;
    }

    private object? ParseEquality(IReadOnlyDictionary<string, object?> vars)
    {
        var left = ParseRelational(vars);
        SkipWhitespace();

        while (true)
        {
            if (Match("=="))
            {
                var right = ParseRelational(vars);
                left = AreEqual(left, right);
            }
            else if (Match("!="))
            {
                var right = ParseRelational(vars);
                left = !AreEqual(left, right);
            }
            else
            {
                break;
            }
            SkipWhitespace();
        }

        return left;
    }

    private object? ParseRelational(IReadOnlyDictionary<string, object?> vars)
    {
        var left = ParseAdditive(vars);
        SkipWhitespace();

        while (true)
        {
            if (Match(">="))
            {
                var right = ParseAdditive(vars);
                left = Compare(left, right) >= 0;
            }
            else if (Match("<="))
            {
                var right = ParseAdditive(vars);
                left = Compare(left, right) <= 0;
            }
            else if (Match(">"))
            {
                var right = ParseAdditive(vars);
                left = Compare(left, right) > 0;
            }
            else if (Match("<"))
            {
                var right = ParseAdditive(vars);
                left = Compare(left, right) < 0;
            }
            else
            {
                break;
            }
            SkipWhitespace();
        }

        return left;
    }

    private object? ParseAdditive(IReadOnlyDictionary<string, object?> vars)
    {
        var left = ParseMultiplicative(vars);
        SkipWhitespace();

        while (true)
        {
            if (Match("+"))
            {
                var right = ParseMultiplicative(vars);
                if (left is string || right is string)
                {
                    left = (left?.ToString() ?? "") + (right?.ToString() ?? "");
                }
                else
                {
                    left = ToDouble(left) + ToDouble(right);
                }
            }
            else if (Match("-"))
            {
                var right = ParseMultiplicative(vars);
                left = ToDouble(left) - ToDouble(right);
            }
            else
            {
                break;
            }
            SkipWhitespace();
        }

        return left;
    }

    private object? ParseMultiplicative(IReadOnlyDictionary<string, object?> vars)
    {
        var left = ParseUnary(vars);
        SkipWhitespace();

        while (true)
        {
            if (Match("*"))
            {
                var right = ParseUnary(vars);
                left = ToDouble(left) * ToDouble(right);
            }
            else if (Match("/"))
            {
                var right = ParseUnary(vars);
                double d = ToDouble(right);
                left = d != 0 ? ToDouble(left) / d : 0.0;
            }
            else if (Match("%"))
            {
                var right = ParseUnary(vars);
                double d = ToDouble(right);
                left = d != 0 ? ToDouble(left) % d : 0.0;
            }
            else
            {
                break;
            }
            SkipWhitespace();
        }

        return left;
    }

    private object? ParseUnary(IReadOnlyDictionary<string, object?> vars)
    {
        SkipWhitespace();

        if (Match("!"))
        {
            var val = ParseUnary(vars);
            return !ToBool(val);
        }

        if (Match("-"))
        {
            var val = ParseUnary(vars);
            return -ToDouble(val);
        }

        if (Match("+"))
        {
            return ParseUnary(vars);
        }

        return ParsePrimary(vars);
    }

    private object? ParsePrimary(IReadOnlyDictionary<string, object?> vars)
    {
        SkipWhitespace();

        if (_pos >= _text.Length) return null;

        // Grouping parentheses
        if (Match("("))
        {
            var inner = ParseExpression(vars);
            SkipWhitespace();
            Match(")");
            return inner;
        }

        // String literal
        if (_text[_pos] == '\"' || _text[_pos] == '\'')
        {
            char quote = _text[_pos++];
            int start = _pos;
            while (_pos < _text.Length && _text[_pos] != quote)
            {
                _pos++;
            }
            string str = _text[start.._pos];
            if (_pos < _text.Length && _text[_pos] == quote) _pos++;
            return str;
        }

        // Numeric literal
        if (char.IsDigit(_text[_pos]) || _text[_pos] == '.')
        {
            int start = _pos;
            bool hasDot = false;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || (_text[_pos] == '.' && !hasDot)))
            {
                if (_text[_pos] == '.') hasDot = true;
                _pos++;
            }
            string numStr = _text[start.._pos];
            if (double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
            {
                return dVal;
            }
            return 0.0;
        }

        // Identifier / variable name / boolean constant
        if (char.IsLetter(_text[_pos]) || _text[_pos] == '_')
        {
            int start = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
            {
                _pos++;
            }
            string id = _text[start.._pos];

            if (id.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (id.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;

            if (vars.TryGetValue(id, out var val))
            {
                return val;
            }

            return 0.0;
        }

        _pos++;
        return null;
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
        {
            _pos++;
        }
    }

    private bool Match(string op)
    {
        SkipWhitespace();
        if (_pos + op.Length <= _text.Length && _text.Substring(_pos, op.Length) == op)
        {
            _pos += op.Length;
            return true;
        }
        return false;
    }

    private static bool ToBool(object? val)
    {
        if (val == null) return false;
        if (val is bool b) return b;
        if (val is double d) return d != 0;
        if (val is int i) return i != 0;
        if (bool.TryParse(val.ToString(), out var pb)) return pb;
        return false;
    }

    private static double ToDouble(object? val)
    {
        if (val == null) return 0;
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        if (val is long l) return l;
        if (val is bool b) return b ? 1.0 : 0.0;
        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var pd)) return pd;
        return 0;
    }

    private static bool AreEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        if (a is bool || b is bool)
        {
            return ToBool(a) == ToBool(b);
        }

        if (a is double or float or int or long || b is double or float or int or long)
        {
            return Math.Abs(ToDouble(a) - ToDouble(b)) < 1e-9;
        }

        return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(object? a, object? b)
    {
        double da = ToDouble(a);
        double db = ToDouble(b);
        return da.CompareTo(db);
    }
}
