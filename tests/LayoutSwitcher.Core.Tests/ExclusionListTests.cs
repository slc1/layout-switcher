using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.Core.Tests;

public class ExclusionListTests
{
    [Theory]
    [InlineData("mstsc.exe", @"C:\Windows\System32\MSTSC.EXE", true)]
    [InlineData("mstsc.exe", @"C:\Tools\notmstsc.exe", false)]
    [InlineData("c:/games/x.exe", @"C:\Games\x.exe", true)]
    [InlineData("c:/games/x.exe", @"D:\Games\x.exe", false)]
    [InlineData(@"C:\Games\x.exe", @"c:/games/X.EXE", true)]
    public void Matches_by_name_or_full_path(string entry, string exePath, bool expected)
    {
        Assert.Equal(expected, new ExclusionList([entry]).IsExcluded(exePath));
    }

    [Fact]
    public void Blank_entries_match_nothing()
    {
        Assert.False(new ExclusionList(["", "  "]).IsExcluded(@"C:\x.exe"));
    }
}
