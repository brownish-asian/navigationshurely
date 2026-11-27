using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using navigationshurely.terminal;
using System.Threading.Tasks;

namespace navigationshurely.models
{
    public class AppSettings
    {
        public string LastImageFilePath { get; set; }
        public int LastBookNumber { get; set; }
        public int LastChapterNumber { get; set; }
        public int LastVerseNumber { get; set; }
        public int LastTranslationNumber { get; set; }

        public Layout LastPreferredLayout { get; set; }

        public bool LastOptionsHint { get; set; }

        public Dictionary<int, Verse> LastMarks { get; set; }

        // Persist the active profile and the complete virtual terminal filesystem.
        public string ActiveProfileName { get; set; } = "default_profile";
        public FileSystemSnapshot TerminalFileSystem { get; set; } = new();

        public AppSettings()
        {
            LastImageFilePath = "images/dbw59.jpg";
            LastBookNumber = 1;
            LastChapterNumber = 1;
            LastVerseNumber = 1;
            LastTranslationNumber = 1;
            LastMarks = new Dictionary<int, Verse>();
            LastPreferredLayout = Layout.Guided;
            LastOptionsHint = true;
        }
    }
}
