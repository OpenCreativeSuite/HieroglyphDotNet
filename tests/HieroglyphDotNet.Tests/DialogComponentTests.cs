using Donkeh.Testing;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Tests;

public sealed class PreviewDockTests
{
    [Fact]
    public void PreviewShowsTabsTextFieldSlidersAndCanvas()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        Assert.Equal(3, page.FindAll(".preview-tab").Count);
        Assert.True(page.FindAll(".preview-tab")[0].HasClass("is-active"));
        Assert.Equal("Handgloves", page.Find(".preview-text").Value);
        Assert.Equal(3, page.FindAll(".mini-slider").Count);
        Assert.Equal(["Size", "Tracking", "Leading"], page.FindAll(".mini-slider-label").Select(label => label.Text));
        Assert.NotNull(page.TryFind(".preview-canvas"));
        Assert.Equal("64", page.FindAll(".mini-slider-value")[0].Text);
    }

    [Fact]
    public void PreviewTabsSwitchAndHideTheTextField()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        page.FindAll(".preview-tab").First(tab => tab.Text == "Alphabet").Click();
        Assert.Equal(PreviewTab.Alphabet, editor.PreviewTab);

        Fx.Reload<PreviewDock>(page, editor);
        Assert.True(page.FindAll(".preview-tab").First(tab => tab.Text == "Alphabet").HasClass("is-active"));
        Assert.Null(page.TryFind(".preview-text"));
    }

    [Fact]
    public void PreviewTextInputUpdatesTheSession()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        page.Input(".preview-text", "Sphinx");

        Assert.Equal("Sphinx", editor.PreviewText);
    }

    [Fact]
    public void PreviewSlidersChangeSizeTrackingAndLeadingWithClamps()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        page.Change(page.FindAll(".mini-slider-input")[0], "120");
        Assert.Equal(120f, editor.PreviewSize);

        page.Change(page.FindAll(".mini-slider-input")[0], "999");
        Assert.Equal(320f, editor.PreviewSize);

        page.Change(page.FindAll(".mini-slider-input")[0], "1");
        Assert.Equal(8f, editor.PreviewSize);

        page.Change(page.FindAll(".mini-slider-input")[1], "40");
        Assert.Equal(40f, editor.PreviewTracking);

        page.Change(page.FindAll(".mini-slider-input")[2], "1.6");
        Assert.Equal(1.6f, editor.PreviewLeading);

        page.Change(page.FindAll(".mini-slider-input")[2], "99");
        Assert.Equal(3f, editor.PreviewLeading);
    }

    [Fact]
    public void ContrastToggleFlipsTheInkMode()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        page.Click("[title='Switch to light ink on charcoal']");
        Assert.True(editor.PreviewDark);

        Fx.Reload<PreviewDock>(page, editor);
        Assert.NotNull(page.TryFind("[title='Switch to dark ink on paper']"));
    }

    [Fact]
    public void CollapseButtonHidesThePreview()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        page.Click("[title='Collapse the preview']");

        Assert.False(editor.ShowPreview);
    }

    [Fact]
    public void PreviewFieldsSuspendShortcuts()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        page.Focus(".preview-text");
        Assert.True(editor.IsTyping);

        page.Blur(".preview-text");
        Assert.False(editor.IsTyping);
    }

    [Fact]
    public void PreviewTabsExposeTheKerningTab()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<PreviewDock>());

        page.FindAll(".preview-tab").First(tab => tab.Text == "Kerning").Click();

        Assert.Equal(PreviewTab.Kerning, editor.PreviewTab);
        Fx.Reload<PreviewDock>(page, editor);
        Assert.True(page.FindAll(".preview-tab")[2].HasClass("is-active"));
    }
}

public sealed class WelcomeDialogTests
{
    [Fact]
    public void WelcomeShowsThePitchStepsAndStarterCaption()
    {
        FontEditor editor = Fx.Sample();
        var page = Fx.Render(editor, C<WelcomeDialog>());

        Assert.Equal("Make a typeface.", page.Find(".welcome-title").Text);
        Assert.Contains("Draw a shape for every letter", page.Find(".welcome-lede").Text);
        Assert.Equal(3, page.FindAll(".welcome-step").Count);
        Assert.Equal(["1", "2", "3"], page.FindAll(".welcome-step-number").Select(step => step.Text));
        Assert.Contains("Occam Regular", page.Find(".welcome-caption").InnerText);
        Assert.Contains("characters drawn", page.Find(".welcome-caption-note").Text);
        Assert.NotNull(page.TryFind(".welcome-specimen"));
    }

    [Fact]
    public void WelcomeActionsOpenTheRightStarts()
    {
        FontEditor editor = Fx.Editor();
        var page = Fx.Render(editor, C<WelcomeDialog>());

        page.FindByText("Create a new font").Click();
        Assert.True(editor.ShowNewFont);

        editor.CloseDialog();
        Fx.Reload<WelcomeDialog>(page, editor);
        page.FindByText("Open a project").Click();
        Assert.True(editor.ShowFileBrowser);
        editor.CloseFileBrowser();

        Fx.Reload<WelcomeDialog>(page, editor);
        page.FindByText("See the starter typeface").Click();
        Assert.Equal("Occam", editor.Project.FamilyName);
        Assert.True(editor.ShowSpecimen);
    }

    [Fact]
    public void WelcomeLinkDismissesTheScreen()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenWelcome();
        var page = Fx.Render(editor, C<WelcomeDialog>());

        page.Find(".welcome-link").Click();

        Assert.False(editor.ShowWelcome);
    }
}

public sealed class NewFontDialogTests
{
    [Fact]
    public void NewFontShowsNameStyleAndBothTemplates()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenNewFont();
        var page = Fx.Render(editor, C<NewFontDialog>());

        Assert.Equal("New font", page.Find(".modal-title").Text);
        Assert.Equal("My Typeface", page.FindAll(".text-field-wide")[0].Value);
        Assert.Equal("Regular", page.FindAll(".text-field-wide")[1].Value);
        Assert.Equal(2, page.FindAll(".template-card").Count);
        Assert.True(page.FindAll(".template-card")[0].HasClass("is-active"));
    }

    [Fact]
    public void CreateBlankBuildsANewProject()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenNewFont();
        var page = Fx.Render(editor, C<NewFontDialog>());

        page.Input(page.FindAll(".text-field-wide")[0], "Fresh Face");
        page.Input(page.FindAll(".text-field-wide")[1], "Book");
        page.FindAll(".btn").First(button => button.Text == "Create font").Click();

        Assert.Equal("Fresh Face", editor.Project.FamilyName);
        Assert.Equal("Book", editor.Style.Name);
        Assert.Equal(0, editor.Project.DrawnCount);
        Assert.False(editor.ShowNewFont);
    }

    [Fact]
    public void ChoosingTheStarterTemplateUsesTheSampleOutlines()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenNewFont();
        var page = Fx.Render(editor, C<NewFontDialog>());

        page.FindAll(".template-card")[1].Click();
        Assert.True(page.FindAll(".template-card")[1].HasClass("is-active"));

        page.FindAll(".btn").First(button => button.Text == "Create font").Click();

        Assert.True(editor.Project.DrawnCount > 60);
        Assert.Equal("My Typeface", editor.Project.FamilyName);
    }

    [Fact]
    public void CancelAndCloseLeaveTheProjectAlone()
    {
        FontEditor editor = Fx.Editor();
        string family = editor.Project.FamilyName;
        editor.OpenNewFont();
        var page = Fx.Render(editor, C<NewFontDialog>());

        page.FindAll(".btn").First(button => button.Text == "Cancel").Click();
        Assert.False(editor.ShowNewFont);
        Assert.Equal(family, editor.Project.FamilyName);

        editor.OpenNewFont();
        Fx.Reload<NewFontDialog>(page, editor);
        page.Click("[title='Close']");
        Assert.False(editor.ShowNewFont);
    }
}

public sealed class ExportDialogTests
{
    [Fact]
    public void ExportDialogListsFormatsIssuesAndTheSuggestedName()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        var page = Fx.Render(editor, C<ExportDialog>());

        Assert.Equal("Export font", page.Find(".modal-title").Text);
        Assert.Equal(4, page.FindAll(".format-card").Count);
        Assert.True(page.FindAll(".format-card")[0].HasClass("is-active"));
        Assert.Equal(["TTF", "OTF", "WOFF", "WOFF2"], page.FindAll(".format-ext").Select(item => item.Text));
        Assert.Equal(editor.ExportName, page.Find(".text-field-wide").Value);
        Assert.NotEmpty(page.FindAll(".issue-row"));
        Assert.Contains("Before you export", page.FindAll(".field-label").Select(label => label.Text).Aggregate((a, b) => a + b));
    }

    [Fact]
    public void ChoosingAFormatReRendersTheActiveCard()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        var page = Fx.Render(editor, C<ExportDialog>());

        page.FindAll(".format-card")[1].Click();
        Assert.Equal(FontFormat.Otf, editor.ExportFormat);

        Fx.Reload<ExportDialog>(page, editor);
        Assert.True(page.FindAll(".format-card")[1].HasClass("is-active"));
        Assert.EndsWith(".otf", page.Find(".text-field-wide").Value);
    }

    [Fact]
    public void FileNameInputUpdatesTheSession()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        var page = Fx.Render(editor, C<ExportDialog>());

        page.Input(".text-field-wide", "MyFont");

        Assert.Equal("MyFont", editor.ExportName);
    }

    [Fact]
    public void StyleChoicesAppearForMultiStyleFamilies()
    {
        FontEditor editor = Fx.Sample();
        editor.AddStyle("Bold", 700, italic: false);
        editor.OpenExport();
        var page = Fx.Render(editor, C<ExportDialog>());

        List<TestElement> styles = [.. page.FindAll(".choice")];
        Assert.Equal(["Regular", "Bold"], styles.Select(choice => choice.Text));

        styles[0].Click();
        Assert.Equal(0, editor.ExportStyleIndex);
    }

    [Fact]
    public void ChoosingALocationOpensTheExportBrowser()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        var page = Fx.Render(editor, C<ExportDialog>());

        page.FindAll(".btn").First(button => button.Text == "Choose location and export").Click();

        Assert.True(editor.ShowFileBrowser);
        Assert.Equal(BrowsePurpose.ExportFont, editor.BrowserPurpose);
    }

    [Fact]
    public void SuccessStateShowsTheWrittenFileAndActions()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.ExportTo(temp.File("done.ttf"));
        var page = Fx.Render(editor, C<ExportDialog>());

        Assert.Equal("Font exported", page.Find(".modal-title").Text);
        Assert.NotNull(page.TryFind(".export-success"));
        Assert.Contains("done.ttf", page.Find(".export-success-file").Text);
        Assert.Equal(editor.LastExportPath, page.Find(".export-success-path").Text);
        Assert.Contains("Export another", page.FindAll(".btn").Select(button => button.InnerText));
        Assert.Contains("Install font", page.FindAll(".btn").Select(button => button.InnerText));
        Assert.Contains("Done", page.FindAll(".btn").Select(button => button.Text));
    }

    [Fact]
    public void SuccessActionsResetCloseOrOpenTheSpecimen()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.ExportTo(temp.File("done.ttf"));
        var page = Fx.Render(editor, C<ExportDialog>());

        page.FindAll(".btn").First(button => button.Text == "Export another").Click();
        Assert.False(editor.ExportComplete);
        Assert.True(editor.ShowExport);

        editor.ExportTo(temp.File("done.ttf"));
        Fx.Reload<ExportDialog>(page, editor);
        page.FindAll(".btn").First(button => button.Text == "Open specimen").Click();
        Assert.True(editor.ShowSpecimen);
        Assert.False(editor.ShowExport);

        editor.OpenExport();
        editor.ExportTo(temp.File("done.ttf"));
        Fx.Reload<ExportDialog>(page, editor);
        page.FindAll(".btn").First(button => button.Text == "Done").Click();
        Assert.False(editor.ShowExport);
    }

    [Fact]
    public void InstallButtonReportsWhyWebFontsCannotBeInstalled()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.SetExportFormat(FontFormat.Woff);
        editor.ExportTo(temp.File("done.woff"));
        var page = Fx.Render(editor, C<ExportDialog>());

        page.FindAll(".btn").First(button => button.InnerText.Contains("Install font")).Click();

        Assert.NotNull(page.TryFind(".export-install"));
        Assert.Contains("Web fonts cannot be installed", page.Find(".export-install").Text);
    }

    [Fact]
    public void CancelClosesWithoutWriting()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        var page = Fx.Render(editor, C<ExportDialog>());

        page.FindAll(".btn").First(button => button.Text == "Cancel").Click();

        Assert.False(editor.ShowExport);
        Assert.Null(editor.LastExportPath);
    }
}

public sealed class StylesDialogTests
{
    [Fact]
    public void StylesDialogListsTheActiveStyleAndAddForm()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenStyles();
        var page = Fx.Render(editor, C<StylesDialog>());

        Assert.Equal("Styles and weights", page.Find(".modal-title").Text);
        Assert.Single(page.FindAll(".style-row"));
        Assert.True(page.FindAll(".style-row")[0].HasClass("is-active"));
        Assert.Contains("Weight 400", page.Find(".style-row-meta").Text);
        Assert.Equal("Bold", page.FindAll(".text-field-wide")[0].Value);
        Assert.Equal("700", page.Find(".style-weight").Value);
        Assert.NotNull(page.TryFind(".btn-tiny"));
    }

    [Fact]
    public void AddingAStyleCopiesTheCurrentOutlines()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenStyles();
        var page = Fx.Render(editor, C<StylesDialog>());

        page.FindAll(".btn").First(button => button.Text == "Add").Click();

        Assert.Equal(2, editor.Project.Styles.Count);
        Assert.Equal("Bold", editor.Style.Name);
        Assert.Equal(700, editor.Style.Weight);
        Assert.True(editor.Style.Glyphs['A'].HasOutline);
    }

    [Fact]
    public void ItalicToggleMarksTheNextStyle()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenStyles();
        var page = Fx.Render(editor, C<StylesDialog>());

        page.Find(".btn-tiny").Click();
        page.FindAll(".btn").First(button => button.Text == "Add").Click();

        Assert.True(editor.Style.Italic);
    }

    [Fact]
    public void ClickingAStyleRowActivatesIt()
    {
        FontEditor editor = Fx.Sample();
        editor.AddStyle("Bold", 700, italic: false);
        editor.SetActiveStyle(0);
        editor.OpenStyles();
        var page = Fx.Render(editor, C<StylesDialog>());

        page.FindAll(".style-row").First(row => row.Find(".style-row-name").Text == "Bold").Click();

        Assert.Equal(1, editor.Project.ActiveStyleIndex);
        Assert.Equal("Bold", editor.Style.Name);
    }

    [Fact]
    public void RenameFieldRenamesTheActiveStyle()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenStyles();
        var page = Fx.Render(editor, C<StylesDialog>());

        page.Change(page.FindAll(".text-field-wide")[1], "Book");

        Assert.Equal("Book", editor.Style.Name);
    }

    [Fact]
    public void DeleteRemovesTheActiveStyleButKeepsOne()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenStyles();
        var page = Fx.Render(editor, C<StylesDialog>());

        Assert.True(page.FindAll(".btn").First(button => button.InnerText.Contains("Delete")).HasAttribute("disabled"));

        editor.AddStyle("Bold", 700, italic: false);
        Fx.Reload<StylesDialog>(page, editor);
        page.FindAll(".btn").First(button => button.InnerText.Contains("Delete")).Click();

        Assert.Single(editor.Project.Styles);
        Assert.Equal("Regular", editor.Style.Name);
    }
}

public sealed class DuplicateDialogTests
{
    [Fact]
    public void DuplicateDialogListsEmptyTargets()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.OpenDuplicateDialog();
        var page = Fx.Render(editor, C<DuplicateDialog>());

        Assert.Equal("Copy A to another character", page.Find(".modal-title").Text);
        Assert.Equal(2, page.FindAll(".choice").Count);
        Assert.True(page.FindAll(".choice")[0].HasClass("is-active"));
        Assert.DoesNotContain(page.FindAll(".dup-cell"), cell => cell.Text == "A");
        Assert.Contains(page.FindAll(".dup-cell"), cell => cell.Text == "B");
    }

    [Fact]
    public void CopyingACellDuplicatesTheOutlineAndSelectsTheTarget()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.OpenDuplicateDialog();
        var page = Fx.Render(editor, C<DuplicateDialog>());

        page.FindAll(".dup-cell").First(cell => cell.Text == "B").Click();

        Assert.Equal('B', editor.SelectedCode);
        Assert.True(editor.SelectedGlyph!.HasOutline);
        Assert.False(editor.ShowDuplicate);
    }

    [Fact]
    public void ShowingAllTargetsIncludesFilledCharacters()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        Fx.DrawRect(editor, 'B', 100, 0, 500, 700);
        editor.OpenDuplicateDialog();
        var page = Fx.Render(editor, C<DuplicateDialog>());

        Assert.DoesNotContain(page.FindAll(".dup-cell"), cell => cell.Text == "A");
        page.FindAll(".choice")[1].Click();
        Assert.Contains(page.FindAll(".dup-cell"), cell => cell.Text == "A");
        Assert.Contains(page.FindAll(".dup-cell"), cell => cell.HasClass("is-full"));
    }

    [Fact]
    public void CancelClosesTheDialog()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.OpenDuplicateDialog();
        var page = Fx.Render(editor, C<DuplicateDialog>());

        page.FindAll(".btn").First(button => button.Text == "Cancel").Click();

        Assert.False(editor.ShowDuplicate);
    }

    [Fact]
    public void DialogRendersNothingWithoutACurrentGlyph()
    {
        FontEditor editor = Fx.Editor();
        editor.Style.Glyphs.Clear();
        var page = Fx.Render(editor, C<DuplicateDialog>());

        Assert.Empty(page.FindAll(".dup-cell"));
        Assert.Null(page.TryFind(".modal-title"));
    }
}

public sealed class AddCharacterDialogTests
{
    [Fact]
    public void AddCharacterStartsWithAUsefulSuggestion()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAddCharacter();
        var page = Fx.Render(editor, C<AddCharacterDialog>());

        Assert.Equal("Add a character", page.Find(".modal-title").Text);
        Assert.Equal("é", page.Find(".text-field-wide").Value);
        Assert.Equal("é", page.Find(".addchar-preview").Text);
        Assert.Contains("U+00E9", page.Find(".addchar-code").Text);
        Assert.Equal(31, page.FindAll(".dup-cell").Count);
        Assert.Contains("Add character", page.FindAll(".btn").Select(button => button.Text));
    }

    [Fact]
    public void HexInputPreviewsTheCharacterAndAddsIt()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAddCharacter();
        var page = Fx.Render(editor, C<AddCharacterDialog>());

        page.Input(".text-field-wide", "U+2603");

        Assert.Equal("☃", page.Find(".addchar-preview").Text);
        Assert.Contains("U+2603", page.Find(".addchar-code").Text);
        Assert.DoesNotContain(page.FindAll(".btn"), button => button.HasAttribute("disabled"));

        page.FindAll(".btn").First(button => button.Text == "Add character").Click();

        Assert.True(editor.Style.Glyphs.ContainsKey(0x2603));
        Assert.Equal(0x2603, editor.SelectedCode);
    }

    [Fact]
    public void ExistingCharactersAreOfferedForSelection()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAddCharacter();
        var page = Fx.Render(editor, C<AddCharacterDialog>());

        page.Input(".text-field-wide", "A");

        Assert.NotNull(page.TryFind(".addchar-exists"));
        Assert.Contains("Select character", page.FindAll(".btn").Select(button => button.Text));

        page.FindAll(".btn").First(button => button.Text == "Select character").Click();

        Assert.Equal('A', editor.SelectedCode);
        Assert.True(editor.Style.Glyphs.ContainsKey('A'));
    }

    [Fact]
    public void InvalidInputDisablesTheAddButton()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAddCharacter();
        var page = Fx.Render(editor, C<AddCharacterDialog>());

        page.Input(".text-field-wide", "U+");

        Assert.Contains(page.FindAll(".btn"), button => button.Text.Contains("Add") && button.HasAttribute("disabled"));
        Assert.Equal("?", page.Find(".addchar-preview").Text);
    }

    [Fact]
    public void SuggestionButtonsFillTheField()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAddCharacter();
        var page = Fx.Render(editor, C<AddCharacterDialog>());

        page.FindAll(".dup-cell").First(cell => cell.Text == "★").Click();

        Assert.Equal("★", page.Find(".addchar-preview").Text);
        Assert.Contains("U+2605", page.Find(".addchar-code").Text);
    }

    [Fact]
    public void CancelClosesWithoutAdding()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAddCharacter();
        var page = Fx.Render(editor, C<AddCharacterDialog>());

        page.FindAll(".btn").First(button => button.Text == "Cancel").Click();

        Assert.False(editor.ShowAddCharacter);
        Assert.False(editor.Style.Glyphs.ContainsKey(0x00E9));
    }
}

public sealed class ShortcutsDialogTests
{
    [Fact]
    public void ShortcutsListsToolsAndEditingGroups()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenShortcuts();
        var page = Fx.Render(editor, C<ShortcutsDialog>());

        Assert.Equal("Keyboard shortcuts", page.Find(".modal-title").Text);
        Assert.Equal(["Tools and view", "Editing"], page.FindAll(".modal-group-title").Select(title => title.Text));
        Assert.Equal(23, page.FindAll(".shortcut-row").Count);
        Assert.Contains(page.FindAll("kbd"), key => key.Text == "V");
        Assert.Contains(page.FindAll("kbd"), key => key.Text == "Ctrl+Z / Ctrl+Y");
    }

    [Fact]
    public void CloseButtonClosesTheSheet()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenShortcuts();
        var page = Fx.Render(editor, C<ShortcutsDialog>());

        page.Click("[title='Close']");

        Assert.False(editor.ShowShortcuts);
    }
}

public sealed class SpecimenDialogTests
{
    [Fact]
    public void SpecimenShowsTheFamilyNameAndCanvas()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenSpecimen();
        var page = Fx.Render(editor, C<SpecimenDialog>());

        Assert.Contains("Occam Regular", page.Find(".modal-title").Text);
        Assert.Contains("specimen", page.Find(".modal-title").Text);
        Assert.NotNull(page.TryFind(".specimen-canvas"));
        Assert.Equal(editor.PreviewText, page.Find(".text-field-wide").Value);
    }

    [Fact]
    public void SpecimenSampleTextUpdatesThePreview()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenSpecimen();
        var page = Fx.Render(editor, C<SpecimenDialog>());

        page.Input(".text-field-wide", "Handgloves");

        Assert.Equal("Handgloves", editor.PreviewText);
    }

    [Fact]
    public void SpecimenExportButtonOpensTheExportDialog()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenSpecimen();
        var page = Fx.Render(editor, C<SpecimenDialog>());

        page.FindAll(".btn-tiny").First(button => button.InnerText.Contains("Export")).Click();

        Assert.True(editor.ShowExport);
    }

    [Fact]
    public void CloseButtonDismissesTheSpecimen()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenSpecimen();
        var page = Fx.Render(editor, C<SpecimenDialog>());

        page.Click("[title='Close']");

        Assert.False(editor.ShowSpecimen);
    }
}

public sealed class AboutDialogTests
{
    [Fact]
    public void AboutListsTheApplicationAndItsValues()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAbout();
        var page = Fx.Render(editor, C<AboutDialog>());

        Assert.Equal("About Hieroglyph.NET", page.Find(".modal-title").Text);
        Assert.Equal("Hieroglyph.NET", page.Find(".about-name").Text);
        Assert.Equal(3, page.FindAll(".about-value").Count);
        Assert.Contains("font", page.Find(".about-copy").Text);
        Assert.Contains("Version 1.0", page.Find(".about-foot").Text);
    }

    [Fact]
    public void CloseButtonDismissesAbout()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenAbout();
        var page = Fx.Render(editor, C<AboutDialog>());

        page.Click("[title='Close']");

        Assert.False(editor.ShowAbout);
    }
}
