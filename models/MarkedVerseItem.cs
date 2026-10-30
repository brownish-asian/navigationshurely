using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace navigationshurely.models
{
    public class MarkedVerseItem
    {
        public int KeyCode { get; set; }
        public Verse Verse { get; set; }

        public string KeyText => ((char)KeyCode).ToString();

        public string VerseText => Verse?.ToString() ?? "";
    }
}
