using System;
using System.IO;
using System.Text.Json;
using RomRebuilderUI.Models;

namespace RomRebuilderUI.Services
{
    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RomRebuilderUI",
            "settings.json"
        );

        public static AppPreferences LoadPreferences()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<AppPreferences>(json) ?? new AppPreferences();
                }
            }
            catch { }
            return new AppPreferences();
        }

        public static void SavePreferences(AppPreferences preferences)
        {
            try
            {
                string? dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}