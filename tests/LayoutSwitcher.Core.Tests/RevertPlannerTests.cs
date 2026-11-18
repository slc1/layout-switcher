using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

public class RevertPlannerTests
{
    private static RevertPlan? Plan(RevertScope scope, IReadOnlyList<TypedKey> keys) =>
        RevertPlanner.Plan(scope, keys, TestLayouts.Pair, "-_");

    [Fact]
    public void Last_word_keeps_trailing_space()
    {
        var plan = Plan(RevertScope.LastWord, TestLayouts.Typed("hello ghbdsn ", LayoutId.En));

        Assert.Equal(new RevertPlan(Start: 6, EraseCount: 7, Text: "привіт ", Target: LayoutId.Ua), plan);
    }

    [Fact]
    public void All_typed_converts_everything()
    {
        var plan = Plan(RevertScope.AllTyped, TestLayouts.Typed("ghbdsn cdsn", LayoutId.En));

        Assert.Equal(new RevertPlan(0, 11, "привіт світ", LayoutId.Ua), plan);
    }

    [Fact]
    public void Second_press_undoes_the_first()
    {
        var buffer = new TypedBuffer(new ManualTimeProvider());
        foreach (var key in TestLayouts.Typed("hello ghbdsn ", LayoutId.En))
            buffer.Add(key);

        var first = Plan(RevertScope.LastWord, buffer.Snapshot())!;
        buffer.MarkDisplayed(first.Start, first.Target);
        var second = Plan(RevertScope.LastWord, buffer.Snapshot());

        Assert.Equal(new RevertPlan(6, 7, "ghbdsn ", LayoutId.En), second);
    }

    [Fact]
    public void All_typed_after_last_word_makes_the_text_consistent()
    {
        // "ghbdsn " still shown in English, "світ" already converted to Ukrainian.
        var keys = TestLayouts.Typed("ghbdsn ", LayoutId.En)
            .Concat(TestLayouts.Type("cdsn", LayoutId.En).Select(s => new TypedKey(s, LayoutId.Ua)))
            .ToList();

        var plan = Plan(RevertScope.AllTyped, keys);

        Assert.Equal(new RevertPlan(0, 11, "привіт світ", LayoutId.Ua), plan);
    }

    [Fact]
    public void Ukrainian_typed_text_targets_english()
    {
        var plan = Plan(RevertScope.LastWord, TestLayouts.Typed("руддщ", LayoutId.Ua));

        Assert.Equal(new RevertPlan(0, 5, "hello", LayoutId.En), plan);
    }

    [Theory]
    [InlineData(RevertScope.LastWord, "123 ")]
    [InlineData(RevertScope.AllTyped, "123 ")]
    [InlineData(RevertScope.LastWord, "")]
    [InlineData(RevertScope.AllTyped, "")]
    public void Nothing_to_convert_gives_no_plan(RevertScope scope, string typed)
    {
        Assert.Null(Plan(scope, TestLayouts.Typed(typed, LayoutId.En)));
    }

    [Fact]
    public void All_typed_target_comes_from_first_letter_not_leading_digits()
    {
        // "1 " typed in English, then the user switched to Ukrainian and typed "руддщ" by mistake.
        var keys = TestLayouts.Typed("1 ", LayoutId.En).Concat(TestLayouts.Typed("руддщ", LayoutId.Ua)).ToList();

        var plan = Plan(RevertScope.AllTyped, keys);

        Assert.Equal(new RevertPlan(0, 7, "1 hello", LayoutId.En), plan);
    }
}
