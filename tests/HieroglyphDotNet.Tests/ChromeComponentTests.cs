using Donkeh.Testing;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Tests;

public sealed class MenuBarTests
{
    [Fact]
    public void MenuBarRendersBrandMenusAndDocumentReadout()
    {
        FontEditor editor = Fx.Sample();
        var page = Fx.Render(editor, C<MenuBar>());

        Assert.Equal("Hieroglyph.NET", page.Find(".brand-name").InnerText);
        Assert.Equal(6, page.FindAll(".menu-title").Count);
        Assert.Contains("Occam", page.Find(".menu-doc-family").Text);
        Assert.Contains("Regular", page.Find(".menu-doc-style").Text);
        Assert.Contains("drawn", page.Find(".menu-doc-count").Text);
        Assert.Equal(
            ["File", "Edit", "Glyph", "View", "Font", "Help"],
            page.FindAll(".menu-title").Select(title => title.Text));
    }

    [Fact]
    public void MenuTitlesOpenToggleAndSweepWithHover()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<MenuBar>());

        page.FindAll(".menu-title").First(title => title.Text == "File").Click();
        Assert.Equal("File", editor.OpenMenu);
        Fx.Reload<MenuBar>(page, editor);
        Assert.True(page.FindAll(".menu-title").First(title => title.Text == "File").HasClass("is-open"));

        page.Hover(page.FindAll(".menu-title").First(title => title.Text == "Edit"));
        Assert.Equal("Edit", editor.OpenMenu);

        page.FindAll(".menu-title").First(title => title.Text == "Edit").Click();
        Assert.Null(editor.OpenMenu);
    }

    [Fact]
    public void FindLooksUpMenusByTitle()
    {
        Assert.NotNull(MenuBar.Find("File"));
        Assert.NotNull(MenuBar.Find("Help"));
        Assert.Null(MenuBar.Find("Nope"));
        Assert.Null(MenuBar.Find(null));
        Assert.All(MenuBar.Menus, menu => Assert.NotEmpty(menu.Items));
    }

    [Fact]
    public void MenuRowsRenderShortcutsGroupsChecksAndDisabledStates()
    {
        FontEditor editor = Fx.Editor();
        editor.ToggleMenuAt("Edit", 0, 0);
        var page = Fx.Render(editor, C<MenuOverlay>());

        Assert.NotNull(page.TryFind(".menu-float"));
        Assert.NotEmpty(page.FindAll(".menu-item"));
        Assert.NotEmpty(page.FindAll(".menu-sep"));
        Assert.Contains(page.FindAll(".menu-item-key"), key => key.Text == "Ctrl+Z");

        TestElement undo = page.FindAll(".menu-item").First(row => row.Find(".menu-item-label").Text == "Undo");
        Assert.True(undo.HasAttribute("disabled"));

        editor.ToggleMenuAt("View", 0, 0);
        Fx.Reload<MenuOverlay>(page, editor);
        TestElement grid = page.FindAll(".menu-item").First(row => row.Find(".menu-item-label").Text == "Show Grid");
        Assert.NotEmpty(grid.FindAll(".fa-check"));
    }

    [Fact]
    public void ClickingAMenuRowRunsTheActionAndClosesTheMenu()
    {
        FontEditor editor = Fx.Editor();
        editor.ToggleMenuAt("View", 0, 0);
        var page = Fx.Render(editor, C<MenuOverlay>());

        page.FindAll(".menu-item")
            .First(row => row.Find(".menu-item-label").Text == "Show Grid")
            .Click();

        Assert.False(editor.ShowGrid);
        Assert.Null(editor.OpenMenu);
    }

    [Fact]
    public void EveryMenuActionCanRunWithoutThrowing()
    {
        foreach (MenuDefinition menu in MenuBar.Menus)
        {
            foreach (MenuAction action in menu.Items)
            {
                FontEditor editor = Fx.Editor();
                Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
                editor.CopyGlyph();
                action.Run(editor);
            }
        }
    }

    [Fact]
    public void OverlayRendersNothingWithoutAnOpenMenu()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<MenuOverlay>());

        Assert.Null(page.TryFind(".menu-float"));
    }

    [Fact]
    public void OverlayPositionsTheMenuAtTheReportedAnchor()
    {
        FontEditor editor = Fx.Editor();
        editor.ToggleMenuAt("Glyph", 42, 56);
        var page = Fx.Render(editor, C<MenuOverlay>());

        string style = page.Find(".menu-float").GetAttribute("style")!;
        Assert.Contains("left:42px", style);
        Assert.Contains("top:56px", style);
        Assert.Contains(page.FindAll(".menu-item-label"), label => label.Text == "Flip Horizontal");
    }
}

public sealed class ToolbarTests
{
    [Fact]
    public void ToolbarShowsFileActionsZoomViewTogglesAndStyleSwitcher()
    {
        FontEditor editor = Fx.Sample();
        var page = Fx.Render(editor, C<Toolbar>());

        Assert.Equal(["New", "Open", "Save", "Export"], page.FindAll(".tb-action").Select(button => button.InnerText));
        Assert.Equal(3, page.FindAll(".options-sep").Count);
        Assert.Contains("Occam", page.Find(".style-switcher-family").Text);
        Assert.Contains("Regular", page.Find(".style-switcher-style").Text);
        Assert.Contains("%", page.Find(".zoom-value").Text);
        Assert.Equal(4, page.FindAll(".is-on").Count);
    }

    [Fact]
    public void FileActionsOpenTheirDialogs()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Toolbar>());

        page.Click("[title='New font (Ctrl+N)']");
        Assert.True(editor.ShowNewFont);
        editor.CloseDialog();

        page.Click("[title='Open a project (Ctrl+O)']");
        Assert.True(editor.ShowFileBrowser);
        Assert.Equal(BrowsePurpose.OpenProject, editor.BrowserPurpose);
        editor.CloseFileBrowser();

        page.Click("[title='Save the project (Ctrl+S)']");
        Assert.True(editor.ShowFileBrowser);
        Assert.Equal(BrowsePurpose.SaveProject, editor.BrowserPurpose);
        editor.CloseFileBrowser();

        page.Click("[title='Export a font file (Ctrl+E)']");
        Assert.True(editor.ShowExport);
    }

    [Fact]
    public void ZoomControlsStepAndFit()
    {
        FontEditor editor = Fx.Editor();
        editor.ZoomAt(0.5f / editor.Zoom, Vec2.Zero);
        var page = Fx.Render(editor, C<Toolbar>());

        page.Click("[title='Zoom in (Ctrl+=)']");
        Assert.Equal(0.65f, editor.Zoom, 3);

        page.Click("[title='Zoom out (Ctrl+-)']");
        Assert.Equal(0.5f, editor.Zoom, 3);

        editor.PanBy(-500, -500);
        page.Find(".zoom-value").Click();
        Assert.NotEqual(-500f, editor.PanX);
    }

    [Fact]
    public void ViewTogglesShowTheirActiveState()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Toolbar>());

        page.Click("[title='Show the design grid']");
        Assert.False(editor.ShowGrid);
        page.Click("[title='Show metric guides']");
        Assert.False(editor.ShowMetrics);
        page.Click("[title='Show sidebearing guides']");
        Assert.False(editor.ShowBearings);
        page.Click("[title='Show or hide the preview panel']");
        Assert.False(editor.ShowPreview);
    }

    [Fact]
    public void UndoAndRedoButtonsDriveTheHistory()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.PushUndo();
        editor.ClearGlyph();
        var page = Fx.Render(editor, C<Toolbar>());

        page.Click("[title='Undo (Ctrl+Z)']");
        Assert.True(editor.SelectedGlyph!.HasOutline);

        page.Click("[title='Redo (Ctrl+Y)']");
        Assert.False(editor.SelectedGlyph!.HasOutline);
    }

    [Fact]
    public void StyleSwitcherOpensTheStyleManager()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<Toolbar>());

        page.Find(".style-switcher").Click();

        Assert.True(editor.ShowStyles);
    }
}

public sealed class StatusBarTests
{
    [Fact]
    public void StatusBarShowsToolCharacterSelectionCursorAndSettings()
    {
        FontEditor editor = Fx.Sample();
        editor.SetTool(GlyphTool.Pen);
        editor.SelectGlyph('Q');
        editor.SetCursor(new Vec2(123, 456), onCanvas: true);
        editor.NotifyStatus("Working");
        editor.SelectNode(0, 0);
        var page = Fx.Render(editor, C<StatusBar>());

        Assert.Equal("Pen", page.Find(".status-tool").Text);
        Assert.Contains("Q", page.Find(".status-item.is-strong").Text);
        Assert.Contains("node", page.FindAll(".status-item").Select(item => item.Text).Aggregate((a, b) => a + b));
        Assert.Contains("123, 456", page.FindAll(".status-item").Select(item => item.Text).Aggregate((a, b) => a + b));
        Assert.Equal("Working", page.Find(".status-message").Text);
        Assert.Contains("1000 upm", page.FindAll(".status-item").Select(item => item.Text));
        Assert.Contains("%", page.FindAll(".status-item").Last().Text);
    }

    [Fact]
    public void StatusBarUsesDashesWhenThePointerIsAway()
    {
        FontEditor editor = Fx.Editor();
        editor.ClearSelection();
        var page = Fx.Render(editor, C<StatusBar>());

        Assert.Contains("—", page.FindAll(".status-item").Select(item => item.Text));
        Assert.Contains("Empty character", page.FindAll(".status-item").Select(item => item.Text));
        Assert.Contains("A", page.Find(".status-item.is-strong").Text);
    }

    [Fact]
    public void StatusBarHandlesNoSelectedGlyph()
    {
        FontEditor editor = Fx.Editor();
        editor.Style.Glyphs.Clear();
        var page = Fx.Render(editor, C<StatusBar>());

        Assert.Equal("No character", page.Find(".status-item.is-strong").Text);
    }
}

public sealed class AppShellTests
{
    [Fact]
    public void AppRendersEveryRegionOfTheWorkspace()
    {
        var page = ComponentTest.Render(C<App>());

        TestElement app = page.Find(".app");
        Assert.NotNull(app);
        Assert.NotNull(page.TryFind(".menubar"));
        Assert.NotNull(page.TryFind(".toolbar"));
        Assert.NotNull(page.TryFind(".browser"));
        Assert.NotNull(page.TryFind(".stage"));
        Assert.NotNull(page.TryFind(".side"));
        Assert.NotNull(page.TryFind(".preview"));
        Assert.NotNull(page.TryFind(".statusbar"));
    }

    [Fact]
    public void AppRegistersGlobalRefreshWithoutThrowing()
    {
        var page = ComponentTest.Render(C<App>());

        page.Update(C<App>());

        Assert.NotNull(page.Find(".app"));
    }
}
