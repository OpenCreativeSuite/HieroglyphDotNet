using Donkeh.Testing;
using Donkeh.UILab;
using HieroglyphDotNet.Components;
using HieroglyphDotNet.Core;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Tests;

public sealed class FileBrowserDialogTests
{
    [Fact]
    public void OpenBrowserListsFoldersAndMatchingProjectFiles()
    {
        using TempDirectory temp = new();
        Directory.CreateDirectory(temp.File("folder"));
        File.WriteAllText(temp.File("one.hgl"), "{}");
        File.WriteAllText(temp.File("two.json"), "{}");
        File.WriteAllText(temp.File("ignored.txt"), "x");
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(temp.Path);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        Assert.Equal("Open project", page.Find(".modal-title").Text);
        Assert.Equal(temp.Path, page.Find(".file-path").Value);
        Assert.Equal(3, page.FindAll(".file-row").Count);
        Assert.Equal("Folder", page.FindAll(".file-row-size")[0].Text);
        Assert.Equal("one.hgl", page.FindAll(".file-row-name")[1].Text);
        Assert.Contains("Open", page.FindAll(".btn").Select(button => button.Text));
    }

    [Fact]
    public void OpenBrowserShowsAnEmptyState()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(temp.Path);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        Assert.NotNull(page.TryFind(".file-empty"));
        Assert.Contains("No project files", page.Find(".file-empty").Text);
        Assert.Null(page.TryFind(".file-row"));
    }

    [Fact]
    public void ClickingAFileRowSelectsItAndMarksTheRow()
    {
        using TempDirectory temp = new();
        File.WriteAllText(temp.File("one.hgl"), "{}");
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(temp.Path);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        page.Find(".file-row").Click();

        Assert.EndsWith("one.hgl", editor.BrowserSelected);
        Assert.Equal("one.hgl", editor.BrowserName);

        Fx.Reload<FileBrowserDialog>(page, editor);
        Assert.True(page.Find(".file-row").HasClass("is-selected"));
    }

    [Fact]
    public void ConfirmingAProjectLoadsIt()
    {
        using TempDirectory temp = new();
        FontProject project = FontProject.Create("From Browser");
        ProjectFile.SaveTo(project, temp.File("browser.hgl"));
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(temp.Path);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        page.Find(".file-row").Click();
        page.FindAll(".btn").First(button => button.Text == "Open").Click();

        Assert.Equal("From Browser", editor.Project.FamilyName);
        Assert.False(editor.ShowFileBrowser);
    }

    [Fact]
    public void SaveBrowserShowsTheSaveEmptyStateAndWritesTheFile()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Editor();
        editor.SaveProjectAs();
        editor.NavigateBrowser(temp.Path);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        Assert.Equal("Save project", page.Find(".modal-title").Text);
        Assert.Contains("Type a name below", page.Find(".file-empty").Text);

        page.Input(".file-name", "saved");
        page.FindAll(".btn").First(button => button.Text == "Save").Click();

        Assert.True(File.Exists(temp.File("saved.hgl")));
        Assert.False(editor.ShowFileBrowser);
    }

    [Fact]
    public void ExportBrowserWritesTheChosenFormat()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Sample();
        editor.OpenExport();
        editor.SetExportFormat(FontFormat.Woff);
        editor.BeginExport();
        editor.NavigateBrowser(temp.Path);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        Assert.Equal("Export font", page.Find(".modal-title").Text);
        Assert.Contains("No matching files", page.Find(".file-empty").Text);

        page.Input(".file-name", "web");
        page.FindAll(".btn").First(button => button.Text == "Export").Click();

        Assert.True(File.Exists(temp.File("web.woff")));
        Assert.True(editor.ExportComplete);
        Assert.False(editor.ShowFileBrowser);
    }

    [Fact]
    public void PathFieldNavigatesToAnExistingFolder()
    {
        using TempDirectory temp = new();
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        page.Change(".file-path", temp.Path);

        Assert.Equal(temp.Path, editor.BrowserFolder);
        Fx.Reload<FileBrowserDialog>(page, editor);
        Assert.Equal(temp.Path, page.Find(".file-path").Value);
    }

    [Fact]
    public void ParentFolderAndPlaceButtonsNavigate()
    {
        using TempDirectory temp = new();
        string child = temp.File("child");
        Directory.CreateDirectory(child);
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(child);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        page.Click("[title='Parent folder']");
        Assert.Equal(temp.Path, editor.BrowserFolder);

        Assert.NotEmpty(page.FindAll(".file-place"));
        TestElement place = page.FindAll(".file-place")[0];
        string path = place.GetAttribute("title")!;
        place.Click();
        Assert.Equal(path, editor.BrowserFolder);
    }

    [Fact]
    public void MissingFoldersReportAStatus()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        editor.NavigateBrowser(missing);

        Assert.Contains("does not exist", editor.BrowserStatus);
    }

    [Fact]
    public void FileSizesAreFormattedReadably()
    {
        using TempDirectory temp = new();
        File.WriteAllBytes(temp.File("small.hgl"), new byte[12]);
        File.WriteAllBytes(temp.File("medium.hgl"), new byte[2048]);
        File.WriteAllBytes(temp.File("large.hgl"), new byte[3 * 1024 * 1024]);
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        editor.NavigateBrowser(temp.Path);
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        List<string> sizes = page.FindAll(".file-row-size").Select(item => item.Text).ToList();
        Assert.Contains("12 B", sizes);
        Assert.Contains("2 KB", sizes);
        Assert.Contains("3 MB", sizes);
    }

    [Fact]
    public void CloseAndCancelBothDismissTheBrowser()
    {
        FontEditor editor = Fx.Editor();
        editor.OpenProject();
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        page.Click("[title='Close']");
        Assert.False(editor.ShowFileBrowser);

        editor.OpenProject();
        Fx.Reload<FileBrowserDialog>(page, editor);
        page.FindAll(".btn").First(button => button.Text == "Cancel").Click();
        Assert.False(editor.ShowFileBrowser);
    }

    [Fact]
    public void NameFieldSuspendsShortcuts()
    {
        FontEditor editor = Fx.Editor();
        editor.SaveProjectAs();
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        page.Focus(".file-name");
        Assert.True(editor.IsTyping);

        page.Blur(".file-name");
        Assert.False(editor.IsTyping);
    }

    [Fact]
    public void ConfirmingWithAnEmptyNameReportsIt()
    {
        FontEditor editor = Fx.Editor();
        editor.SaveProjectAs();
        var page = Fx.Render(editor, C<FileBrowserDialog>());

        page.Input(".file-name", " ");
        page.FindAll(".btn").First(button => button.Text == "Save").Click();

        Assert.Contains("Enter a file name", editor.BrowserStatus);
        Assert.True(editor.ShowFileBrowser);
    }
}
