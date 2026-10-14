using VisionStudio.Core.Expressions;

namespace VisionStudio.Core.Tests.Expressions;

public sealed class ExpressionEvaluatorTests
{
    [Theory]
    [InlineData("1 + 2 * 3", 7.0)]
    [InlineData("(1 + 2) * 3", 9.0)]
    [InlineData("10 / 2 + 5", 10.0)]
    [InlineData("100 % 30", 10.0)]
    public void Arithmetic_CalculatesPrecedenceAccurately(string expr, double expected)
    {
        var res = ExpressionEvaluator.EvaluateDouble(expr, new Dictionary<string, object?>());
        res.Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData("Score > 0.8", true)]
    [InlineData("Score >= 0.95", true)]
    [InlineData("Score < 0.5", false)]
    [InlineData("Area > 500 && Circularity > 0.7", true)]
    [InlineData("Area > 2000 || Circularity > 0.9", false)]
    [InlineData("!IsDefect", true)]
    public void Conditions_EvaluateCorrectlyWithVariables(string expr, bool expected)
    {
        var vars = new Dictionary<string, object?>
        {
            ["Score"] = 0.95,
            ["Area"] = 1200.0,
            ["Circularity"] = 0.85,
            ["IsDefect"] = false
        };

        var res = ExpressionEvaluator.EvaluateBoolean(expr, vars);
        res.Should().Be(expected);
    }
}
