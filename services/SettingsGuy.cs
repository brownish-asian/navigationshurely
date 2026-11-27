using navigationshurely.models;
using System;
using System.IO;
using System.Text.Json;

namespace navigationshurely.services
{
    public class SettingsGuy
    {
        private static readonly string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "navigationshurely");

        private static readonly string filePath = Path.Combine(folderPath, "settings.json");

        public SettingsGuy() { }

        public void saVe(AppSettings guy)
        {
            ArgumentNullException.ThrowIfNull(guy);

            Directory.CreateDirectory(folderPath);

            string appJson = JsonSerializer.Serialize(
                guy,
                new JsonSerializerOptions { WriteIndented = true });

            // Write beside the destination, then replace it. A failed write
            // therefore cannot leave a half-written settings.json behind.
            string temporaryPath = filePath + ".tmp";

            try
            {
                File.WriteAllText(temporaryPath, appJson);
                File.Move(temporaryPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public AppSettings loAd()
        {
            if (!File.Exists(filePath))
                return new AppSettings();

            try
            {
                string appJson = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<AppSettings>(appJson)
                    ?? new AppSettings();
            }
            catch (Exception)
            {
                return new AppSettings();
            }
        }
    }
} 