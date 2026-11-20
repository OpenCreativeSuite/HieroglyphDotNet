using Donkeh.Testing;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Tests;

public sealed class CharacterBrowserTests
{
    [Fact]
    public void BrowserShowsProgressChipsSearchAndEveryCharacter()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        var page = Fx.Render(editor, C<CharacterBrowser>());

        Assert.Equal("Characters", page.Find(".browser-title").Text);
        Assert.Equal($"1 / {editor.Project.CharacterCount}", page.Find(".browser-progress").Text);
        Assert.Equal(6, page.FindAll(".chip").Count);
        Assert.True(page.FindAll(".chip")[0].HasClass("is-active"));
        Assert.Equal(editor.Project.CharacterCount, page.FindAll(".char-tile").Count);
        Assert.NotNull(page.Find(".browser-search-input"));
    }

    [Fact]
    public void CategoryChipsFilterAndToggleOff()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<CharacterBrowser>());

        TestElement uppercase = page.FindAll(".chip").First(chip => chip.Find(".chip-label").Text == "Uppercase");
        uppercase.Click();
        Assert.Equal(CharacterCategory.Uppercase, editor.BrowserCategory);

        Fx.Reload<CharacterBrowser>(page, editor);
        Assert.Equal(26, page.FindAll(".char-tile").Count);

        page.FindAll(".chip").First(chip => chip.Find(".chip-label").Text == "Uppercase").Click();
        Assert.Null(editor.BrowserCategory);
        Fx.Reload<CharacterBrowser>(page, editor);
        Assert.Equal(editor.Project.CharacterCount, page.FindAll(".char-tile").Count);
    }

    [Fact]
    public void CustomChipIsHiddenUntilACustomCharacterExists()
    {
        FontEditor editor = Fx.Editor();
        Assert.DoesNotContain(
            Fx.Render(editor, C<CharacterBrowser>()).FindAll(".chip"),
            chip => chip.Find(".chip-label").Text == "Custom");

        editor.AddCharacter(0x2603);
        var page = Fx.Render(editor, C<CharacterBrowser>());

        Assert.Contains(page.FindAll(".chip"), chip => chip.Find(".chip-label").Text == "Custom");
    }

    [Fact]
    public void SearchFiltersTilesAndShowsTheEmptyState()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<CharacterBrowser>());

        page.Input(".browser-search-input", "Capital B");
        Assert.Equal("Capital B", editor.BrowserSearch);
        Fx.Reload<CharacterBrowser>(page, editor);
        Assert.Single(page.FindAll(".char-tile"));

        page.Input(".browser-search-input", "nothing-here-42");
        Fx.Reload<CharacterBrowser>(page, editor);
        Assert.Empty(page.FindAll(".char-tile"));
        Assert.NotNull(page.TryFind(".browser-empty"));
    }

    [Fact]
    public void ClickingATileSelectsTheCharacter()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<CharacterBrowser>());

        TestElement tile = page.FindAll(".char-tile").First(item => item.GetAttribute("title")!.Contains("Capital B"));
        tile.Click();

        Assert.Equal('B', editor.SelectedCode);
        Assert.Equal("B", editor.SelectedGlyph!.Character);

        Fx.Reload<CharacterBrowser>(page, editor);
        Assert.True(page.FindAll(".char-tile").First(item => item.GetAttribute("title")!.Contains("Capital B")).HasClass("is-selected"));
        Assert.True(page.FindAll(".char-tile").First(item => item.GetAttribute("title")!.Contains("Capital A")).HasClass("is-blank"));
    }

    [Fact]
    public void DrawnTilesAndTitlesDescribeTheirState()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        var page = Fx.Render(editor, C<CharacterBrowser>());

        TestElement a = page.FindAll(".char-tile").First(item => item.GetAttribute("title")!.Contains("Capital A"));
        TestElement b = page.FindAll(".char-tile").First(item => item.GetAttribute("title")!.Contains("Capital B"));

        Assert.True(a.HasClass("is-drawn"));
        Assert.Contains("U+0041", a.GetAttribute("title"));
        Assert.DoesNotContain("still blank", a.GetAttribute("title")!);
        Assert.Contains("still blank", b.GetAttribute("title"));
    }

    [Fact]
    public void BlankEyeFilterAndAddCharacterButtonWork()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        var page = Fx.Render(editor, C<CharacterBrowser>());

        page.Click("[title='Show only characters that still need drawing']");
        Assert.True(editor.BrowserOnlyBlank);
        Fx.Reload<CharacterBrowser>(page, editor);
        Assert.DoesNotContain(page.FindAll(".char-tile"), tile => tile.GetAttribute("title")!.Contains("Capital A"));

        page.Find(".browser-add").Click();
        Assert.True(editor.ShowAddCharacter);
    }

    [Fact]
    public void SearchFieldsSuspendAndRestoreShortcuts()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<CharacterBrowser>());

        page.Focus(".browser-search-input");
        Assert.True(editor.IsTyping);

        page.Blur(".browser-search-input");
        Assert.False(editor.IsTyping);
    }
}
