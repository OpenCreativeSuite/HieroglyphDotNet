using System.IO;
using System.Text.Json;

namespace HieroglyphDotNet.Core;

/// <summary>Small preferences that survive between launches.</summary>
public sealed class Settings
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Whether the welcome screen has been dismissed before.</summary>
    public bool WelcomeSeen { get; set; }

    /// <summary>The folder last used by the file browser.</summary>
    public string LastFolder { get; set; } = "";

    /// <summary>The folder preferences are stored in.</summary>
    public static string Folder
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Hieroglyph");
        }
    }

    private static string FilePath => Path.Combine(Folder, "settings.json");

    /// <summary>Loads the settings, returning defaults when no file exists.</summary>
    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                Settings? loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // A broken preferences file must never stop the application.
        }

        return new Settings();
    }

    /// <summary>Writes the settings, ignoring failures.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // Preferences are a convenience; failure is not worth interrupting.
        }
    }
}
