using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Tests;

public sealed class FontEditorMenuTests
{
    [Fact]
    public void TogglingTheSameMenuClosesIt()
    {
        FontEditor editor = Fx.Editor();

        editor.ToggleMenuAt("File", 12, 34);
        Assert.Equal("File", editor.OpenMenu);
        Assert.Equal(12, editor.MenuX);
        Assert.Equal(34, editor.MenuY);

        editor.ToggleMenuAt("File", 50, 60);
        Assert.Null(editor.OpenMenu);
    }

    [Fact]
    public void HoverSweepsMenusOnlyWhileOneIsOpen()
    {
        FontEditor editor = Fx.Editor();

        editor.HoverMenuAt("Edit", 30, 40);
        Assert.Null(editor.OpenMenu);
        Assert.Equal(0, editor.MenuX);

        editor.ToggleMenuAt("File", 10, 20);
        editor.HoverMenuAt("Edit", 40, 50);

        Assert.Equal("Edit", editor.OpenMenu);
        Assert.Equal(40, editor.MenuX);
        Assert.Equal(50, editor.MenuY);
    }

    [Fact]
    public void CloseMenuIsIdempotent()
    {
        FontEditor editor = Fx.Editor();
        editor.CloseMenu();
        Assert.Null(editor.OpenMenu);

        editor.ToggleMenuAt("View", 1, 2);
        editor.CloseMenu();
        Assert.Null(editor.OpenMenu);
    }
}

public sealed class FontEditorDialogTests
{
    [Fact]
    public void EveryDialogOpensAndClosesItsMenu()
    {
        FontEditor editor = Fx.Editor();

        editor.ToggleMenuAt("File", 10, 10);
        editor.OpenNewFont();
        Assert.True(editor.ShowNewFont);
        Assert.Null(editor.OpenMenu);

        editor.OpenStyles();
        Assert.True(editor.ShowStyles);
        Assert.Null(editor.OpenMenu);

        editor.OpenAddCharacter();
        Assert.True(editor.ShowAddCharacter);

        editor.OpenShortcuts();
        Assert.True(editor.ShowShortcuts);

        editor.OpenSpecimen();
        Assert.True(editor.ShowSpecimen);

        editor.OpenAbout();
        Assert.True(editor.ShowAbout);
    }

    [Fact]
    public void OpenDuplicateRequiresAnOutline()
    {
        FontEditor editor = Fx.Editor();

        editor.OpenDuplicateDialog();
        Assert.False(editor.ShowDuplicate);
        Assert.Contains("Draw a character", editor.Status);

        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.OpenDuplicateDialog();
        Assert.True(editor.ShowDuplicate);
    }

    [Fact]
    public void CloseDialogClearsEveryTransientSurface()
    {
        FontEditor editor = Fx.Editor();
        Fx.DrawRect(editor, 'A', 100, 0, 500, 700);
        editor.OpenNewFont();
        editor.OpenExport();
        editor.OpenSpecimen();
        editor.OpenStyles();
        editor.OpenShortcuts();
        editor.OpenDuplicateDialog();
        editor.OpenAddCharacter();
        editor.OpenAbout();
        editor.OpenProject();
        editor.ToggleMenuAt("File", 1, 2);

        editor.CloseDialog();

        Assert.False(editor.ShowNewFont);
        Assert.False(editor.ShowExport);
        Assert.False(editor.ShowSpecimen);
        Assert.False(editor.ShowStyles);
        Assert.False(editor.ShowShortcuts);
        Assert.False(editor.ShowDuplicate);
        Assert.False(editor.ShowAddCharacter);
        Assert.False(editor.ShowAbout);
        Assert.False(editor.ShowFileBrowser);
        Assert.Null(editor.OpenMenu);
    }

    [Fact]
    public void WelcomeCanBeOpenedAndDismissed()
    {
        FontEditor editor = Fx.Editor();
        editor.ToggleMenuAt("Help", 1, 2);

        editor.OpenWelcome();

        Assert.True(editor.ShowWelcome);
        Assert.Null(editor.OpenMenu);

        editor.CloseWelcome();

        Assert.False(editor.ShowWelcome);
    }
}

public sealed class FontEditorExportTests
{
    [Fact]
    public void OpenExportValidatesAndSuggestsAName()
    {
        FontEditor editor = Fx.Sample();

        editor.OpenExport();

        Assert.True(editor.ShowExport);
        Assert.Equal(FontFormat.Ttf, editor.ExportFormat);
        Assert.EndsWith(".ttf", editor.ExportName);
        Assert.NotEmpty(editor.ExportIssues);
        Assert.True(FontValidator.CanExport(editor.ExportIssues));
        Assert.False(editor.ExportComplete);
    }

    [Fact]
    public void ChangingTheFormatUpdatesTheSuggestedExtension()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();

        editor.SetExportFormat(FontFormat.Otf);
        Assert.EndsWith(".otf", editor.ExportName);
        Assert.Equal(FontFormat.Otf, editor.ExportFormat);

        editor.SetExportFormat(FontFormat.Woff);
        Assert.EndsWith(".woff", editor.ExportName);

        editor.SetExportFormat(FontFormat.Woff2);
        Assert.EndsWith(".woff2", editor.ExportName);
    }

    [Fact]
    public void ChangingTheExportStyleRevalidatesAndRenames()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.AddStyle("Bold", 700, italic: false);

        editor.SetExportStyle(1);

        Assert.Equal(1, editor.ExportStyleIndex);
        Assert.Contains("Bold", editor.ExportName);
        Assert.True(FontValidator.CanExport(editor.ExportIssues));

        editor.SetExportStyle(99);
        Assert.Equal(1, editor.ExportStyleIndex);

        editor.SetExportStyle(-5);
        Assert.Equal(0, editor.ExportStyleIndex);
    }

    [Fact]
    public void BeginExportOpensTheSaveBrowserForAValidFont()
    {
        FontEditor editor = Fx.Sample();
        editor.OpenExport();

        editor.BeginExport();

        Assert.True(editor.ShowFileBrowser);
        Assert.Equal(BrowsePurpose.ExportFont, editor.BrowserPurpose);
        Assert.False(editor.ShowExport);
        Assert.EndsWith(".ttf", editor.BrowserName);
    }

    [Fact]
    public void BeginExportStopsWhenValidationFails()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenExport();

        editor.BeginExport();

        Assert.False(editor.ShowFileBrowser);
        Assert.True(editor.ShowExport);
        Assert.False(FontValidator.CanExport(editor.ExportIssues));
    }

    [Fact]
    public void ExportToWritesEveryFormatWithTheRightSignature()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();

        foreach (FontFormat format in Enum.GetValues<FontFormat>())
        {
            editor.OpenExport();
            editor.SetExportFormat(format);
            string path = temp.File("font" + FontExporter.Extension(format));

            Assert.True(editor.ExportTo(path));

            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000);
            Assert.Equal(path, editor.LastExportPath);
            Assert.True(editor.ExportComplete);
            Assert.Contains(Path.GetFileName(path), editor.ExportMessage);
            Assert.False(editor.ShowFileBrowser);
            Assert.True(editor.ShowExport);
        }
    }

    [Fact]
    public void ExportToReportsFailures()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.OpenExport();

        bool ok = editor.ExportTo(temp.Path);

        Assert.False(ok);
        Assert.False(editor.ExportComplete);
        Assert.Contains("Export failed", editor.ExportMessage);
        Assert.Null(editor.LastExportPath);
    }

    [Fact]
    public void InstallExplainsWhenThereIsNothingToInstall()
    {
        FontEditor editor = Fx.Editor();

        Assert.Equal("Export a font first.", editor.InstallLastExport());
    }

    [Fact]
    public void WebFontsCannotBeInstalled()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.SetExportFormat(FontFormat.Woff);
        editor.ExportTo(temp.File("font.woff"));

        string message = editor.InstallLastExport();

        Assert.Contains("Web fonts cannot be installed", message);
    }

    [Fact]
    public void RevealExportWithoutAFileDoesNothing()
    {
        FontEditor editor = Fx.Editor();

        editor.RevealExport();

        Assert.Null(editor.LastExportPath);
    }
}

public sealed class FontEditorProjectFileTests
{
    [Fact]
    public void SaveProjectWithoutAPathOpensSaveAs()
    {
        FontEditor editor = Fx.Editor();

        editor.SaveProject();

        Assert.True(editor.ShowFileBrowser);
        Assert.Equal(BrowsePurpose.SaveProject, editor.BrowserPurpose);
        Assert.Equal("Save project", editor.BrowserTitle);
        Assert.Equal("Save", editor.BrowserConfirm);
        Assert.EndsWith(".hgl", editor.BrowserName);
    }

    [Fact]
    public void SaveProjectWritesToAKnownPath()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        string path = temp.File("direct.hgl");
        editor.Project.Path = path;
        editor.Project.Dirty = true;

        editor.SaveProject();

        Assert.True(File.Exists(path));
        Assert.False(editor.Project.Dirty);
        Assert.False(editor.ShowFileBrowser);
        Assert.Contains("Saved direct.hgl", editor.Status);
    }

    [Fact]
    public void SaveProjectAsSuggestsTheFamilyName()
    {
        FontEditor editor = Fx.Editor();
        editor.Project.FamilyName = "Fancy Face";

        editor.SaveProjectAs();

        Assert.True(editor.ShowFileBrowser);
        Assert.Equal("Fancy Face.hgl", editor.BrowserName);
    }

    [Fact]
    public void SaveProjectAsUsesUntitledForABlankFamily()
    {
        FontEditor editor = Fx.Editor();
        editor.Project.FamilyName = "   ";

        editor.SaveProjectAs();

        Assert.Equal("Untitled.hgl", editor.BrowserName);
    }

    [Fact]
    public void OpenProjectOpensTheBrowser()
    {
        FontEditor editor = Fx.Editor();

        editor.OpenProject();

        Assert.True(editor.ShowFileBrowser);
        Assert.Equal(BrowsePurpose.OpenProject, editor.BrowserPurpose);
        Assert.Equal("Open project", editor.BrowserTitle);
        Assert.Equal("Open", editor.BrowserConfirm);
        Assert.False(editor.BrowserSaveMode);
    }

    [Fact]
    public void LoadProjectReplacesTheSessionAndSelectsA()
    {
        using TempDirectory temp = new();
        FontEditor source = Fx.Sample();
        source.Project.FamilyName = "Loaded Face";
        string path = temp.File("loaded.hgl");
        ProjectFile.SaveTo(source.Project, path);

        FontEditor editor = Fx.Editor();
        editor.LoadProject(path);

        Assert.Equal("Loaded Face", editor.Project.FamilyName);
        Assert.Equal('A', editor.SelectedCode);
        Assert.True(editor.SelectedGlyph!.HasOutline);
        Assert.False(editor.ShowFileBrowser);
        Assert.Contains("Opened loaded.hgl", editor.Status);
    }

    [Fact]
    public void LoadProjectAlwaysHasTheStandardRepertoireToSelect()
    {
        using TempDirectory temp = new();
        string path = temp.File("symbols.hgl");
        FontProject project = FontProject.Create("Symbols");
        project.ActiveStyle.Glyphs.Clear();
        project.ActiveStyle.Glyphs[0x2603] = Glyph.Blank(0x2603, "Snowman", 600);
        ProjectFile.SaveTo(project, path);

        FontEditor editor = Fx.Editor();
        editor.LoadProject(path);

        Assert.Equal('A', editor.SelectedCode);
        Assert.True(editor.Style.Glyphs.ContainsKey(0x2603));
    }

    [Fact]
    public void LoadProjectReportsBrokenFilesAndKeepsTheCurrentProject()
    {
        using TempDirectory temp = new();
        string path = temp.File("broken.hgl");
        File.WriteAllText(path, "not a project");
        FontEditor editor = Fx.Editor();
        string family = editor.Project.FamilyName;

        editor.LoadProject(path);

        Assert.Equal(family, editor.Project.FamilyName);
        Assert.Contains("Could not open the project", editor.BrowserStatus);
    }

    [Fact]
    public void BrowserListsOnlyMatchingFilesAndFoldersFirst()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(temp.File("folder"));
        File.WriteAllText(temp.File("one.hgl"), "{}");
        File.WriteAllText(temp.File("two.json"), "{}");
        File.WriteAllText(temp.File("ignored.txt"), "x");
        FontEditor editor = Fx.Editor();
        editor.NavigateBrowser(temp.Path);

        Assert.Equal(temp.Path, editor.BrowserFolder);
        Assert.Equal(3, editor.BrowserEntries.Count);
        Assert.Equal("folder", editor.BrowserEntries[0].Name);
        Assert.True(editor.BrowserEntries[0].IsFolder);
        Assert.Contains(editor.BrowserEntries, entry => entry.Name == "one.hgl");
        Assert.Contains(editor.BrowserEntries, entry => entry.Name == "two.json");
        Assert.DoesNotContain(editor.BrowserEntries, entry => entry.Name == "ignored.txt");
    }

    [Fact]
    public void BrowserClickNavigatesFoldersAndSelectsFiles()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(temp.File("folder"));
        File.WriteAllText(temp.File("one.hgl"), "{}");
        FontEditor editor = Fx.Editor();
        editor.NavigateBrowser(temp.Path);

        FileEntry folder = editor.BrowserEntries.First(entry => entry.IsFolder);
        editor.BrowserClickEntry(folder);
        Assert.Equal(folder.Path, editor.BrowserFolder);

        editor.NavigateBrowser(temp.Path);
        FileEntry file = editor.BrowserEntries.First(entry => !entry.IsFolder);
        editor.BrowserClickEntry(file);

        Assert.Equal(file.Path, editor.BrowserSelected);
        Assert.Equal(file.Name, editor.BrowserName);
    }

    [Fact]
    public void BrowserConfirmRequiresAProjectFile()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(temp.Path);

        editor.BrowserConfirmEntry();

        Assert.Contains("Choose a project file", editor.BrowserStatus);
        Assert.True(editor.ShowFileBrowser);
    }

    [Fact]
    public void BrowserConfirmLoadsTheSelectedProject()
    {
        using TempDirectory temp = new();
        FontProject project = FontProject.Create("Browser Load");
        string path = temp.File("browser.hgl");
        ProjectFile.SaveTo(project, path);

        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(temp.Path);
        editor.BrowserClickEntry(editor.BrowserEntries.First(entry => entry.Name == "browser.hgl"));

        editor.BrowserConfirmEntry();

        Assert.Equal("Browser Load", editor.Project.FamilyName);
        Assert.False(editor.ShowFileBrowser);
    }

    [Fact]
    public void SavingFromTheBrowserAppendsTheProjectExtension()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Editor();
        editor.SaveProjectAs();
        editor.NavigateBrowser(temp.Path);
        editor.BrowserName = "myfont";

        editor.BrowserConfirmEntry();

        Assert.True(File.Exists(temp.File("myfont.hgl")));
        Assert.Equal(temp.File("myfont.hgl"), editor.Project.Path);
        Assert.False(editor.ShowFileBrowser);
    }

    [Fact]
    public void SavingFromTheBrowserKeepsAnExistingExtension()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Editor();
        editor.SaveProjectAs();
        editor.NavigateBrowser(temp.Path);
        editor.BrowserName = "already.hgl";

        editor.BrowserConfirmEntry();

        Assert.True(File.Exists(temp.File("already.hgl")));
        Assert.False(File.Exists(temp.File("already.hgl.hgl")));
    }

    [Fact]
    public void EmptyNamesAndMissingFoldersAreReported()
    {
        FontEditor editor = Fx.Editor();
        editor.SaveProjectAs();
        editor.BrowserName = "   ";

        editor.BrowserConfirmEntry();
        Assert.Contains("Enter a file name", editor.BrowserStatus);

        editor.NavigateBrowser(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Contains("does not exist", editor.BrowserStatus);
    }

    [Fact]
    public void ExportFromTheBrowserAppendsTheFormatExtension()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.BeginExport();
        editor.NavigateBrowser(temp.Path);
        editor.BrowserName = "browserfont";

        editor.BrowserConfirmEntry();

        Assert.True(File.Exists(temp.File("browserfont.ttf")));
        Assert.True(editor.ExportComplete);
        Assert.False(editor.ShowFileBrowser);
        Assert.True(editor.ShowExport);
    }

    [Fact]
    public void ExportBrowserFiltersToTheChosenFormat()
    {
        using TempDirectory temp = new();
        File.WriteAllText(temp.File("old.ttf"), "x");
        File.WriteAllText(temp.File("other.otf"), "x");
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.SetExportFormat(FontFormat.Ttf);
        editor.BeginExport();
        editor.NavigateBrowser(temp.Path);

        Assert.Contains(editor.BrowserEntries, entry => entry.Name == "old.ttf");
        Assert.DoesNotContain(editor.BrowserEntries, entry => entry.Name == "other.otf");
    }

    [Fact]
    public void BrowserNavigationSupportsParentsAndPlaces()
    {
        using TempDirectory temp = new();
        string child = temp.File("child");
        Directory.CreateDirectory(child);
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(child);

        editor.BrowserGoUp();
        Assert.Equal(temp.Path, editor.BrowserFolder);

        Assert.NotEmpty(editor.BrowserPlaces);
        (string label, string path) = editor.BrowserPlaces[0];
        Assert.False(string.IsNullOrWhiteSpace(label));
        editor.BrowserUsePlace(path);
        Assert.Equal(path, editor.BrowserFolder);
    }

    [Fact]
    public void ClosingTheBrowserHidesIt()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenProject();

        editor.CloseFileBrowser();

        Assert.False(editor.ShowFileBrowser);
        Assert.False(editor.BrowserSaveMode);
    }
}
