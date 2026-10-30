using navigationshurely.models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace navigationshurely.services
{
    public class SettingsGuy
    {
        private static readonly string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "navigationshurely");
        private static readonly string filePath = Path.Combine(folderPath, "settings.json");

        public SettingsGuy() { }

        public void saVe(AppSettings guy)
        {
            Directory.CreateDirectory(folderPath);
            string appJson = JsonSerializer.Serialize(guy, new JsonSerializerOptions { WriteIndented = true});
            File.WriteAllText(filePath, appJson);
        }

        public AppSettings loAd()
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string appJson = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<AppSettings>(appJson)?? new AppSettings();
                }
                catch (Exception)
                {
                    return new AppSettings();
                }
            }
            return new AppSettings();
        }
    }
}
