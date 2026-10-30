using BibleApp.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace navigationshurely.models
{
    public class Verse : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _selected;

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;

                _selected = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(BorderColor));
            }
        }

        private BibleDatabase database;

        private int translationId;

        public int BookId { get; set; }

        public int Chapter { get; set; }

        public int VerseNumber { get; set; }

        public string Text { get; set; } = "";


        [JsonIgnore]
        public Brush BorderColor =>
            Selected ? Brushes.Red : Brushes.Transparent;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            String result = Title() + " " + Chapter.ToString() + ":" + VerseNumber.ToString();
            return result;
        }

        public string Title()
        {
            database = BibleDatabase.Master();
            translationId = database.TranslationNumber();
            if (translationId == 1)
            {
                switch (BookId)
                {
                    case 1: return "GENESIS";
                    case 2: return "EXODO";
                    case 3: return "LEVITICO";
                    case 4: return "BILANG";
                    case 5: return "DEUTERONOMIO";
                    case 6: return "JOSUE";
                    case 7: return "HUKOM";
                    case 8: return "RUTH";
                    case 9: return "I SAMUEL";
                    case 10: return "II SAMUEL";
                    case 11: return "I HARI";
                    case 12: return "II HARI";
                    case 13: return "I CRONICA";
                    case 14: return "II CRONICA";
                    case 15: return "EZRA";
                    case 16: return "NEHEMIAS";
                    case 17: return "ESTHER";
                    case 18: return "JOB";
                    case 19: return "AWIT";
                    case 20: return "KAWIKAAN";
                    case 21: return "ECLESIASTES";
                    case 22: return "AWIT NG MGA AWIT";
                    case 23: return "ISAIAS";
                    case 24: return "JEREMIAS";
                    case 25: return "PANAGHOY";
                    case 26: return "EZEKIEL";
                    case 27: return "DANIEL";
                    case 28: return "OSEAS";
                    case 29: return "JOEL";
                    case 30: return "AMOS";
                    case 31: return "OBADIAS";
                    case 32: return "JONAS";
                    case 33: return "MIKAS";
                    case 34: return "NAHUM";
                    case 35: return "HABACUC";
                    case 36: return "ZEFANIAS";
                    case 37: return "HAGAI";
                    case 38: return "ZACARIAS";
                    case 39: return "MALAKIAS";

                    case 40: return "MATEO";
                    case 41: return "MARCOS";
                    case 42: return "LUCAS";
                    case 43: return "JUAN";
                    case 44: return "GAWA";
                    case 45: return "ROMA";
                    case 46: return "I CORINTO";
                    case 47: return "II CORINTO";
                    case 48: return "GALACIA";
                    case 49: return "EFESO";
                    case 50: return "FILIPOS";
                    case 51: return "COLOSAS";
                    case 52: return "I TESALONICA";
                    case 53: return "II TESALONICA";
                    case 54: return "I TIMOTEO";
                    case 55: return "II TIMOTEO";
                    case 56: return "TITO";
                    case 57: return "FILEMON";
                    case 58: return "HEBREO";
                    case 59: return "SANTIAGO";
                    case 60: return "I PEDRO";
                    case 61: return "II PEDRO";
                    case 62: return "I JUAN";
                    case 63: return "II JUAN";
                    case 64: return "III JUAN";
                    case 65: return "JUDAS";
                    case 66: return "APOCALIPSIS";

                    default:
                        return "HINDI KILALANG AKLAT";
                }
            }
            else if (translationId == 2)
            {
                switch (BookId)
                {
                    case 1: return "GENESIS";
                    case 2: return "EXODUS";
                    case 3: return "LEVITICUS";
                    case 4: return "NUMBERS";
                    case 5: return "DEUTERONOMY";
                    case 6: return "JOSHUA";
                    case 7: return "JUDGES";
                    case 8: return "RUTH";
                    case 9: return "I SAMUEL";
                    case 10: return "II SAMUEL";
                    case 11: return "I KINGS";
                    case 12: return "II KINGS";
                    case 13: return "I CHRONICLES";
                    case 14: return "II CHRONICLES";
                    case 15: return "EZRA";
                    case 16: return "NEHEMIAH";
                    case 17: return "ESTHER";
                    case 18: return "JOB";
                    case 19: return "PSALMS";
                    case 20: return "PROVERBS";
                    case 21: return "ECCLESIASTES";
                    case 22: return "SONG OF SONGS";
                    case 23: return "ISAIAH";
                    case 24: return "JEREMIAH";
                    case 25: return "LAMENTATIONS";
                    case 26: return "EZEKIEL";
                    case 27: return "DANIEL";
                    case 28: return "HOSEA";
                    case 29: return "JOEL";
                    case 30: return "AMOS";
                    case 31: return "OBADIAH";
                    case 32: return "JONAH";
                    case 33: return "MICAH";
                    case 34: return "NAHUM";
                    case 35: return "HABAKKUK";
                    case 36: return "ZEPHANIAH";
                    case 37: return "HAGGAI";
                    case 38: return "ZECHARIAH";
                    case 39: return "MALACHI";

                    case 40: return "MATTHEW";
                    case 41: return "MARK";
                    case 42: return "LUKE";
                    case 43: return "JOHN";
                    case 44: return "ACTS";
                    case 45: return "ROMANS";
                    case 46: return "I CORINTHIANS";
                    case 47: return "II CORINTHIANS";
                    case 48: return "GALACIANS";
                    case 49: return "EPHESIANS";
                    case 50: return "PHILIPPIANS";
                    case 51: return "COLOSSIANS";
                    case 52: return "I THESSALONIANS";
                    case 53: return "II THESSALONIANS";
                    case 54: return "I TIMOTHY";
                    case 55: return "II TIMOTHY";
                    case 56: return "TITUS";
                    case 57: return "PHILEMON";
                    case 58: return "HEBREWS";
                    case 59: return "JAMES";
                    case 60: return "I PETER";
                    case 61: return "II PETER";
                    case 62: return "I JOHN";
                    case 63: return "II JOHN";
                    case 64: return "III JOHN";
                    case 65: return "JUDAH";
                    case 66: return "REVELATION";

                    default:
                        return "don't know the book bro";
                }
            }
            else
            {
                return "don't know the translation";
            }
        }
    }
}
