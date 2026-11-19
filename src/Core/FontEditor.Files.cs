using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace HieroglyphDotNet.Core;

/// <summary>What the in-app file browser is being used for.</summary>
public enum BrowsePurpose
{
    /// <summary>Open an existing project.</summary>
    OpenProject,

    /// <summary>Save the project.</summary>
    SaveProject,

    /// <summary>Write a compiled font file.</summary>
    ExportFont,
}

/// <summary>One row in the file browser.</summary>
public sealed record FileEntry(string Name, string Path, bool IsFolder, long Size);

/// <summary>Dialogs, the file browser, saving, exporting and installing.</summary>
public sealed partial class FontEditor
{
    private readonly List<FileEntry> _browserEntries = [];
    private readonly List<(string Label, string Path)> _browserPlaces = [];
    private string _lastFolder = "";
    private string? _lastExportPath;

    // ---------------------------------------------------------------- menus

    /// <summary>The open menu title, or null.</summary>
    public string? OpenMenu { get; private set; }

    /// <summary>Left position of the open menu.</summary>
    public float MenuX { get; private set; }

    /// <summary>Top position of the open menu.</summary>
    public float MenuY { get; private set; } = 28f;

    /// <summary>Whether a text field owns the keyboard.</summary>
    public bool IsTyping { get; set; }

    /// <summary>Opens or closes a menu under a title.</summary>
    public void ToggleMenuAt(string title, float x, float y)
    {
        if (OpenMenu == title)
        {
            OpenMenu = null;
            Notify();
            return;
        }

        OpenMenu = title;
        MenuX = x;
        MenuY = y;
        Notify();
    }

    /// <summary>Switches the open menu as the pointer sweeps the bar.</summary>
    public void HoverMenuAt(string title, float x, float y)
    {
        if (OpenMenu is null)
        {
            return;
        }

        MenuX = x;
        MenuY = y;
        if (OpenMenu != title)
        {
            OpenMenu = title;
            Notify();
        }
    }

    /// <summary>Closes any open menu.</summary>
    public void CloseMenu()
    {
        if (OpenMenu is null)
        {
            return;
        }

        OpenMenu = null;
        Notify();
    }

    // -------------------------------------------------------------- dialogs

    /// <summary>Whether the welcome screen is showing.</summary>
    public bool ShowWelcome { get; private set; }

    /// <summary>Whether the new font dialog is showing.</summary>
    public bool ShowNewFont { get; private set; }

    /// <summary>Whether the export dialog is showing.</summary>
    public bool ShowExport { get; private set; }

    /// <summary>Whether the specimen viewer is showing.</summary>
    public bool ShowSpecimen { get; private set; }

    /// <summary>Whether the style manager is showing.</summary>
    public bool ShowStyles { get; private set; }

    /// <summary>Whether the shortcut sheet is showing.</summary>
    public bool ShowShortcuts { get; private set; }

    /// <summary>Whether the duplicate-to-character dialog is showing.</summary>
    public bool ShowDuplicate { get; private set; }

    /// <summary>Whether the add-character dialog is showing.</summary>
    public bool ShowAddCharacter { get; private set; }

    /// <summary>Whether the about dialog is showing.</summary>
    public bool ShowAbout { get; private set; }

    /// <summary>Whether the file browser is showing.</summary>
    public bool ShowFileBrowser { get; private set; }

    /// <summary>Closes every transient dialog.</summary>
    public void CloseDialog()
    {
        ShowNewFont = false;
        ShowExport = false;
        ShowSpecimen = false;
        ShowStyles = false;
        ShowShortcuts = false;
        ShowDuplicate = false;
        ShowAddCharacter = false;
        ShowAbout = false;
        ShowFileBrowser = false;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Shows the welcome screen.</summary>
    public void OpenWelcome()
    {
        ShowWelcome = true;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Hides the welcome screen and remembers that it has been seen.</summary>
    public void CloseWelcome()
    {
        DismissWelcome();
        Notify();
    }

    private void DismissWelcome()
    {
        ShowWelcome = false;
        if (!_settings.WelcomeSeen)
        {
            _settings.WelcomeSeen = true;
            _settings.Save();
        }
    }

    /// <summary>Opens the new font dialog.</summary>
    public void OpenNewFont()
    {
        ShowNewFont = true;
        DismissWelcome();
        OpenMenu = null;
        Notify();
    }

    /// <summary>Opens the style manager.</summary>
    public void OpenStyles()
    {
        ShowStyles = true;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Opens the duplicate dialog.</summary>
    public void OpenDuplicateDialog()
    {
        if (SelectedGlyph?.HasOutline != true)
        {
            NotifyStatus("Draw a character before copying it to another one.");
            return;
        }

        ShowDuplicate = true;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Opens the add-character dialog.</summary>
    public void OpenAddCharacter()
    {
        ShowAddCharacter = true;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Opens the shortcut sheet.</summary>
    public void OpenShortcuts()
    {
        ShowShortcuts = true;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Opens the specimen viewer.</summary>
    public void OpenSpecimen()
    {
        ShowSpecimen = true;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Opens the about dialog.</summary>
    public void OpenAbout()
    {
        ShowAbout = true;
        OpenMenu = null;
        Notify();
    }

    // -------------------------------------------------------------- export

    /// <summary>The container chosen in the export dialog.</summary>
    public FontFormat ExportFormat { get; private set; } = FontFormat.Ttf;

    /// <summary>The file name proposed by the export dialog.</summary>
    public string ExportName { get; set; } = "";

    /// <summary>The style index being exported.</summary>
    public int ExportStyleIndex { get; set; }

    /// <summary>Findings from the last validation run.</summary>
    public List<FontIssue> ExportIssues { get; private set; } = [];

    /// <summary>A message shown after a successful export.</summary>
    public string ExportMessage { get; private set; } = "";

    /// <summary>The path written by the last export.</summary>
    public string? LastExportPath => _lastExportPath;

    /// <summary>Whether the dialog is showing its success state.</summary>
    public bool ExportComplete { get; private set; }

    /// <summary>Opens the export dialog and validates the current style.</summary>
    public void OpenExport()
    {
        ExportComplete = false;
        ExportMessage = "";
        ExportStyleIndex = Project.ActiveStyleIndex;
        ExportFormat = FontFormat.Ttf;
        ExportName = FontExporter.SuggestedFileName(Project, Style, ExportFormat);
        ExportIssues = FontValidator.Validate(Project, Style);
        ShowExport = true;
        OpenMenu = null;
        Notify();
    }

    /// <summary>Chooses the export container.</summary>
    public void SetExportFormat(FontFormat format)
    {
        ExportFormat = format;
        ExportName = FontExporter.SuggestedFileName(Project, Project.Styles[Math.Clamp(ExportStyleIndex, 0, Project.Styles.Count - 1)], format);
        Notify();
    }

    /// <summary>Chooses which style to compile.</summary>
    public void SetExportStyle(int index)
    {
        ExportStyleIndex = Math.Clamp(index, 0, Project.Styles.Count - 1);
        ExportName = FontExporter.SuggestedFileName(Project, Project.Styles[ExportStyleIndex], ExportFormat);
        ExportIssues = FontValidator.Validate(Project, Project.Styles[ExportStyleIndex]);
        Notify();
    }

    /// <summary>Re-validates and opens the save browser.</summary>
    public void BeginExport()
    {
        ExportIssues = FontValidator.Validate(Project, Project.Styles[ExportStyleIndex]);
        if (!FontValidator.CanExport(ExportIssues))
        {
            Notify();
            return;
        }

        OpenBrowser(BrowsePurpose.ExportFont, ExportName);
    }

    /// <summary>Compiles and writes the font.</summary>
    public bool ExportTo(string path)
    {
        try
        {
            FontStyle style = Project.Styles[Math.Clamp(ExportStyleIndex, 0, Project.Styles.Count - 1)];
            byte[] bytes = FontExporter.Compile(Project, style, ExportFormat);
            File.WriteAllBytes(path, bytes);
            _lastExportPath = path;
            ExportMessage = $"{Path.GetFileName(path)}  ·  {Size(bytes.Length)}";
            ExportComplete = true;
            ShowFileBrowser = false;
            ShowExport = true;
            NotifyStatus($"Exported {Path.GetFileName(path)}");
            Notify();
            return true;
        }
        catch (Exception error)
        {
            BrowserStatus = "Could not write the font: " + error.Message;
            ExportMessage = "Export failed: " + error.Message;
            ExportComplete = false;
            Notify();
            return false;
        }
    }

    /// <summary>Installs the last exported font for the current user.</summary>
    public string InstallLastExport()
    {
        if (_lastExportPath is null || !File.Exists(_lastExportPath))
        {
            return "Export a font first.";
        }

        if (!OperatingSystem.IsWindows())
        {
            return "Automatic install is only available on Windows. Open the file and use your system font tool.";
        }

        string extension = Path.GetExtension(_lastExportPath).ToLowerInvariant();
        if (extension is ".woff" or ".woff2")
        {
            return "Web fonts cannot be installed. Export TTF or OTF to install this typeface.";
        }

        try
        {
            string fonts = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            Directory.CreateDirectory(fonts);

            string destination = Path.Combine(fonts, Path.GetFileName(_lastExportPath));
            File.Copy(_lastExportPath, destination, overwrite: true);

            string label = $"{Project.FamilyName} {Style.Name} ({(extension == ".otf" ? "OpenType" : "TrueType")})";
            using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows NT\CurrentVersion\Fonts");
            key.SetValue(label, destination);

            BroadcastFontChange();
            return $"{Project.FamilyName} {Style.Name} installed. Apps pick it up the next time they start.";
        }
        catch (Exception error)
        {
            return "Install failed: " + error.Message;
        }
    }

    /// <summary>Reveals the last export in Explorer.</summary>
    public void RevealExport()
    {
        if (_lastExportPath is null || !File.Exists(_lastExportPath))
        {
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastExportPath}\"") { UseShellExecute = true });
            }
            else
            {
                string? folder = Path.GetDirectoryName(_lastExportPath);
                if (folder is not null)
                {
                    Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                }
            }
        }
        catch
        {
            // Opening a folder is a convenience; a failure is not worth a dialog.
        }
    }

    private static string Size(long bytes) => bytes < 1024
        ? bytes + " bytes"
        : bytes < 1024 * 1024
            ? (bytes / 1024f).ToString("0.#") + " KB"
            : (bytes / (1024f * 1024f)).ToString("0.#") + " MB";

    // --------------------------------------------------------------- project

    /// <summary>Opens the save browser for the project file.</summary>
    public void SaveProject()
    {
        if (Project.Path is null)
        {
            SaveProjectAs();
            return;
        }

        WriteProject(Project.Path);
    }

    /// <summary>Opens the save browser with a suggested file name.</summary>
    public void SaveProjectAs()
    {
        string name = string.IsNullOrWhiteSpace(Project.FamilyName) ? "Untitled" : Project.FamilyName;
        OpenBrowser(BrowsePurpose.SaveProject, name + ProjectFile.Extension);
    }

    /// <summary>Opens the file browser for a project file.</summary>
    public void OpenProject()
    {
        OpenBrowser(BrowsePurpose.OpenProject, "");
    }

    /// <summary>Loads a project from disk.</summary>
    public void LoadProject(string path)
    {
        try
        {
            FontProject project = ProjectFile.Load(path);
            Project = project;
            _selectedCode = project.ActiveStyle.Glyphs.ContainsKey('A')
                ? 'A'
                : project.ActiveStyle.Glyphs.Keys.OrderBy(code => code).FirstOrDefault();
            _undo.Clear();
            _redo.Clear();
            ClearSelection();
            _raster.Clear();
            _geometryVersion++;
            ShowFileBrowser = false;
            DismissWelcome();
            ShowStarterPanel = SelectedGlyph?.HasOutline != true;
            _viewInitialised = false;
            FitGlyph(silent: true);
            NotifyStatus($"Opened {Path.GetFileName(path)}");
        }
        catch (Exception error)
        {
            BrowserStatus = "Could not open the project: " + error.Message;
            Notify();
        }
    }

    private void WriteProject(string path)
    {
        try
        {
            string folder = Path.GetDirectoryName(path) ?? "";
            if (folder.Length > 0 && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            ProjectFile.SaveTo(Project, path);
            ShowFileBrowser = false;
            NotifyStatus($"Saved {Path.GetFileName(path)}");
            Notify();
        }
        catch (Exception error)
        {
            BrowserStatus = "Could not save the project: " + error.Message;
            Notify();
        }
    }

    // ---------------------------------------------------------- file browser

    /// <summary>The browser title.</summary>
    public string BrowserTitle => BrowserPurpose switch
    {
        BrowsePurpose.SaveProject => "Save project",
        BrowsePurpose.ExportFont => "Export font",
        _ => "Open project",
    };

    /// <summary>The browser confirm label.</summary>
    public string BrowserConfirm => BrowserPurpose switch
    {
        BrowsePurpose.SaveProject => "Save",
        BrowsePurpose.ExportFont => "Export",
        _ => "Open",
    };

    /// <summary>What the browser was opened for.</summary>
    public BrowsePurpose BrowserPurpose { get; private set; }

    /// <summary>Whether the browser is choosing a destination.</summary>
    public bool BrowserSaveMode => BrowserPurpose != BrowsePurpose.OpenProject;

    /// <summary>The folder shown.</summary>
    public string BrowserFolder { get; private set; } = "";

    /// <summary>The file name field.</summary>
    public string BrowserName { get; set; } = "";

    /// <summary>The selected file path.</summary>
    public string BrowserSelected { get; private set; } = "";

    /// <summary>Status or error text.</summary>
    public string BrowserStatus { get; set; } = "";

    /// <summary>The listed rows.</summary>
    public IReadOnlyList<FileEntry> BrowserEntries => _browserEntries;

    /// <summary>The shortcut folders.</summary>
    public IReadOnlyList<(string Label, string Path)> BrowserPlaces => _browserPlaces;

    private void OpenBrowser(BrowsePurpose purpose, string name)
    {
        BrowserPurpose = purpose;
        BrowserSelected = "";
        BrowserName = name;
        BrowserStatus = "";

        string folder = _lastFolder;
        if (folder.Length == 0 || !Directory.Exists(folder))
        {
            folder = DefaultFolder();
        }

        BrowserFolder = folder;
        BuildPlaces();
        RefreshBrowser();
        ShowFileBrowser = true;
        OpenMenu = null;
        ShowExport = false;
        Notify();
    }

    /// <summary>Moves the browser to a folder.</summary>
    public void NavigateBrowser(string folder)
    {
        try
        {
            string full = Path.GetFullPath(folder.Trim().Trim('"'));
            if (!Directory.Exists(full))
            {
                BrowserStatus = "That folder does not exist.";
                Notify();
                return;
            }

            BrowserFolder = full;
            _lastFolder = full;
            _settings.LastFolder = full;
            _settings.Save();
            BrowserSelected = "";
            BrowserStatus = "";
            RefreshBrowser();
            Notify();
        }
        catch (Exception error)
        {
            BrowserStatus = error.Message;
            Notify();
        }
    }

    /// <summary>Moves to the parent folder.</summary>
    public void BrowserGoUp()
    {
        DirectoryInfo? parent = Directory.GetParent(BrowserFolder);
        if (parent is not null)
        {
            NavigateBrowser(parent.FullName);
        }
    }

    /// <summary>Uses one of the shortcut folders.</summary>
    public void BrowserUsePlace(string path) => NavigateBrowser(path);

    /// <summary>Handles a click on a row.</summary>
    public void BrowserClickEntry(FileEntry entry)
    {
        if (entry.IsFolder)
        {
            NavigateBrowser(entry.Path);
            return;
        }

        BrowserSelected = entry.Path;
        BrowserName = entry.Name;
        BrowserStatus = "";
        Notify();
    }

    /// <summary>Confirms the browser choice.</summary>
    public void BrowserConfirmEntry()
    {
        if (BrowserPurpose == BrowsePurpose.OpenProject)
        {
            if (!File.Exists(BrowserSelected))
            {
                BrowserStatus = "Choose a project file.";
                Notify();
                return;
            }

            LoadProject(BrowserSelected);
            return;
        }

        string name = BrowserName.Trim();
        if (name.Length == 0)
        {
            BrowserStatus = "Enter a file name.";
            Notify();
            return;
        }

        if (BrowserPurpose == BrowsePurpose.ExportFont)
        {
            string extension = FontExporter.Extension(ExportFormat);
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                name += extension;
                BrowserName = name;
            }
        }
        else if (!name.EndsWith(ProjectFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            name += ProjectFile.Extension;
            BrowserName = name;
        }

        string path = Path.Combine(BrowserFolder, name);
        if (BrowserPurpose == BrowsePurpose.ExportFont)
        {
            ExportTo(path);
        }
        else
        {
            WriteProject(path);
        }
    }

    /// <summary>Closes the file browser.</summary>
    public void CloseFileBrowser()
    {
        ShowFileBrowser = false;
        Notify();
    }

    private string[] BrowserExtensions => BrowserPurpose switch
    {
        BrowsePurpose.ExportFont => [FontExporter.Extension(ExportFormat)],
        _ => ProjectFile.Extensions,
    };

    private void RefreshBrowser()
    {
        _browserEntries.Clear();
        BrowserStatus = "";

        try
        {
            foreach (string directory in Directory.GetDirectories(BrowserFolder))
            {
                DirectoryInfo info = new(directory);
                if ((info.Attributes & FileAttributes.Hidden) != 0)
                {
                    continue;
                }

                _browserEntries.Add(new FileEntry(info.Name, info.FullName, true, 0));
            }

            string[] extensions = BrowserExtensions;
            foreach (string file in Directory.GetFiles(BrowserFolder))
            {
                string extension = Path.GetExtension(file);
                if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileInfo info = new(file);
                _browserEntries.Add(new FileEntry(info.Name, info.FullName, false, info.Length));
            }

            _browserEntries.Sort((a, b) => a.IsFolder != b.IsFolder
                ? (a.IsFolder ? -1 : 1)
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception error)
        {
            BrowserStatus = "Cannot read this folder: " + error.Message;
        }
    }

    private void BuildPlaces()
    {
        _browserPlaces.Clear();
        Add("Documents", Environment.SpecialFolder.MyDocuments);
        Add("Desktop", Environment.SpecialFolder.DesktopDirectory);
        Add("Downloads", Environment.SpecialFolder.UserProfile, "Downloads");
        Add("Home", Environment.SpecialFolder.UserProfile);

        void Add(string label, Environment.SpecialFolder folder, string? child = null)
        {
            string path = Environment.GetFolderPath(folder);
            if (child is not null)
            {
                path = Path.Combine(path, child);
            }

            if (Directory.Exists(path))
            {
                _browserPlaces.Add((label, path));
            }
        }
    }

    private static string DefaultFolder()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents) ? documents : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);

    private static void BroadcastFontChange() =>
        SendMessageTimeout(new IntPtr(0xFFFF), 0x001D, IntPtr.Zero, IntPtr.Zero, 0x0002, 1000, out _);
}
