using navigationshurely.models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

namespace navigationshurely.terminal
{
    public class Config : Data, INotifyPropertyChanged
    {
        private double _top;
        private double _left;
        private int _size;

        // NaN represents WPF's default Auto sizing.
        private double _width = double.NaN;
        private double _height = double.NaN;

        private double _fontSize = 12;
        private double _contentScale = 1;

        private Visibility _visibility = Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Controls Identifier { get; }

        public double Top
        {
            get => _top;
            set
            {
                ValidateFiniteNumber(nameof(Top), value);
                SetField(ref _top, value);
            }
        }

        public double Left
        {
            get => _left;
            set
            {
                ValidateFiniteNumber(nameof(Left), value);
                SetField(ref _left, value);
            }
        }

        public double Width
        {
            get => _width;
            set
            {
                ValidateDimension(nameof(Width), value);
                SetField(ref _width, value);
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                ValidateDimension(nameof(Height), value);
                SetField(ref _height, value);
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                ValidateFiniteNumber(nameof(FontSize), value);

                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(FontSize),
                        "FontSize must be greater than zero.");
                }

                SetField(ref _fontSize, value);
            }
        }

        /// <summary>
        /// Uniform scale applied to composite content such as list templates.
        /// Baseline typography remains in XAML; the profile stores only this factor.
        /// </summary>
        public double ContentScale
        {
            get => _contentScale;
            set
            {
                ValidateFiniteNumber(nameof(ContentScale), value);

                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(ContentScale),
                        "ContentScale must be greater than zero.");
                }

                SetField(ref _contentScale, value);
            }
        }

        public Visibility Visibility
        {
            get => _visibility;
            set
            {
                if (!Enum.IsDefined(
                    typeof(Visibility),
                    value))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(Visibility));
                }

                SetField(ref _visibility, value);
            }
        }

        // Retained for compatibility with the previous Config format.
        public int Size
        {
            get => _size;
            set => SetField(ref _size, value);
        }

        public Config(Controls id) : base(id.ToString())
        {
            Identifier = id;
            _title = id + ".config";

            // Defaults taken from MainWindow.xaml.
            switch (id)
            {
                case Controls.TitleBox:
                    _left = 96;
                    _top = 90;
                    _fontSize = 70;
                    break;

                case Controls.TranslationBox:
                    _left = 96;
                    _top = 162;
                    _fontSize = 40;
                    break;

                case Controls.FrequencyBox:
                    _left = 1540;
                    _top = 990;
                    _width = 340;
                    _fontSize = 17;
                    break;

                case Controls.AliasBox:
                    _left = 0;
                    _top = 1030;
                    _width = 1920;
                    _fontSize = 17;
                    break;

                case Controls.ModeBox:
                    _left = 1580;
                    _top = 1022;
                    _width = 300;
                    _fontSize = 24;
                    break;

                case Controls.VerseBox:
                    _left = 96;
                    _top = 230;
                    _width = 1728;
                    _height = 800;
                    break;

                case Controls.GuidedHud:
                    _left = 1480;
                    _top = 990;
                    _width = 400;
                    _height = 90;
                    break;

                case Controls.RightList:
                    _left = 1830;
                    _top = 173;
                    _width = 90;
                    _height = 827;
                    break;

                case Controls.BottomList:
                    _left = 30;
                    _top = 946;
                    // The original Border has Auto width and a 50-unit
                    // maximum-height ListView. The viewport supplies a small
                    // minimum width so it remains selectable while hidden.
                    // 58 units covers the ListView maximum plus Border chrome.
                    _width = double.NaN;
                    _height = 58;
                    break;
            }
        }

        public override void Rename(string newName)
        {
            SetFileName(newName);
        }

        public override string Info
        {
            get
            {
                return
                    $"Control={Identifier}\n" +
                    $"Top={FormatNumber(Top)}\n" +
                    $"Left={FormatNumber(Left)}\n" +
                    $"Width={FormatDimension(Width)}\n" +
                    $"Height={FormatDimension(Height)}\n" +
                    $"FontSize={FormatNumber(FontSize)}\n" +
                    $"ContentScale={FormatNumber(ContentScale)}\n" +
                    $"Visibility={Visibility}\n" +
                    $"Size={Size}";
            }
        }

        // ----------------------------------------------------
        // POSITION UPDATES
        // ----------------------------------------------------

        public void SetPosition(double? left, double? top)
        {
            if (!left.HasValue && !top.HasValue)
            {
                throw new ArgumentException(
                    "At least one coordinate must be supplied.");
            }

            // Validate every supplied coordinate before changing either.
            if (left.HasValue)
                ValidateFiniteNumber(nameof(Left), left.Value);

            if (top.HasValue)
                ValidateFiniteNumber(nameof(Top), top.Value);

            // Unspecified coordinates remain unchanged.
            if (left.HasValue)
                Left = left.Value;

            if (top.HasValue)
                Top = top.Value;
        }

        /// <summary>
        /// Commits a complete edited rectangle. Every supplied value is
        /// validated before any property is changed, so an invalid drag cannot
        /// leave half of a rectangle updated.
        /// </summary>
        public void SetLayout(
            double left,
            double top,
            double width,
            double height,
            double? fontSize = null,
            double? contentScale = null)
        {
            ValidateFiniteNumber(nameof(Left), left);
            ValidateFiniteNumber(nameof(Top), top);
            ValidateDimension(nameof(Width), width);
            ValidateDimension(nameof(Height), height);

            if (fontSize.HasValue)
            {
                ValidateFiniteNumber(nameof(FontSize), fontSize.Value);
                if (fontSize.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(fontSize));
            }

            if (contentScale.HasValue)
            {
                ValidateFiniteNumber(nameof(ContentScale), contentScale.Value);
                if (contentScale.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(contentScale));
            }

            Left = left;
            Top = top;
            Width = width;
            Height = height;

            if (fontSize.HasValue)
                FontSize = fontSize.Value;

            if (contentScale.HasValue)
                ContentScale = contentScale.Value;
        }

        // ----------------------------------------------------
        // CONFIGURATION FILE
        // ----------------------------------------------------

        public void ChangeInfo(string info)
        {
            ArgumentNullException.ThrowIfNull(info);

            double newTop = Top;
            double newLeft = Left;
            double newWidth = Width;
            double newHeight = Height;
            double newFontSize = FontSize;
            double newContentScale = ContentScale;

            Visibility newVisibility = Visibility;
            int newSize = Size;

            if (string.IsNullOrWhiteSpace(info))
            {
                Config defaults = new Config(Identifier);

                Top = defaults.Top;
                Left = defaults.Left;
                Width = defaults.Width;
                Height = defaults.Height;
                FontSize = defaults.FontSize;
                ContentScale = defaults.ContentScale;
                Visibility = defaults.Visibility;
                Size = defaults.Size;

                return;
            }

            string[] lines = info
                .Replace("\r\n", "\n")
                .Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("#"))
                {
                    continue;
                }

                int separator = line.IndexOf('=');

                if (separator <= 0)
                {
                    throw new FormatException(
                        $"Invalid configuration line: {line}");
                }

                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();

                switch (key.ToLowerInvariant())
                {
                    case "control":
                        if (!string.Equals(
                            value,
                            Identifier.ToString(),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new FormatException(
                                "A configuration cannot be assigned to another control.");
                        }

                        break;

                    case "top":
                        newTop = ParseFiniteDouble(key, value);
                        break;

                    case "left":
                        newLeft = ParseFiniteDouble(key, value);
                        break;

                    case "width":
                        newWidth = ParseDimension(key, value);
                        break;

                    case "height":
                        newHeight = ParseDimension(key, value);
                        break;

                    case "fontsize":
                        newFontSize = ParsePositiveDouble(key, value);
                        break;

                    case "contentscale":
                        newContentScale = ParsePositiveDouble(key, value);
                        break;

                    case "visibility":
                        newVisibility = ParseVisibility(value);
                        break;

                    // Legacy property.
                    case "size":
                        newSize = ParseInteger(key, value);
                        break;

                    default:
                        throw new FormatException(
                            $"Unknown configuration property: {key}");
                }
            }

            // All input has been validated. Now commit the changes.
            Top = newTop;
            Left = newLeft;
            Width = newWidth;
            Height = newHeight;
            FontSize = newFontSize;
            ContentScale = newContentScale;
            Visibility = newVisibility;
            Size = newSize;
        }

        public void SetValue(string property, string value)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(value);

            switch (property.ToLowerInvariant())
            {
                case "top":
                    Top = ParseFiniteDouble(property, value);
                    break;

                case "left":
                    Left = ParseFiniteDouble(property, value);
                    break;

                case "width":
                    Width = ParseDimension(property, value);
                    break;

                case "height":
                    Height = ParseDimension(property, value);
                    break;

                case "fontsize":
                    FontSize = ParsePositiveDouble(property, value);
                    break;

                case "contentscale":
                    ContentScale = ParsePositiveDouble(property, value);
                    break;

                case "visibility":
                    Visibility = ParseVisibility(value);
                    break;

                case "size":
                    Size = ParseInteger(property, value);
                    break;

                default:
                    throw new ArgumentException(
                        $"Unknown configuration property: {property}");
            }
        }

        // ----------------------------------------------------
        // PROPERTY CHANGE NOTIFICATION
        // ----------------------------------------------------

        private bool SetField<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;

            OnPropertyChanged(propertyName);

            return true;
        }

        protected virtual void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }

        // ----------------------------------------------------
        // VALIDATION AND FORMATTING
        // ----------------------------------------------------

        private static void ValidateFiniteNumber(
            string property,
            double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    property,
                    $"{property} must contain a finite number.");
            }
        }

        private static void ValidateDimension(
            string property,
            double value)
        {
            // NaN represents WPF Auto sizing.
            if (double.IsNaN(value))
                return;

            if (double.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    property,
                    $"{property} must be Auto or a non-negative number.");
            }
        }

        private static double ParseFiniteDouble(
            string property,
            string value)
        {
            if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double result) ||
                double.IsNaN(result) ||
                double.IsInfinity(result))
            {
                throw new FormatException(
                    $"{property} must contain a valid finite number.");
            }

            return result;
        }

        private static double ParsePositiveDouble(
            string property,
            string value)
        {
            double result = ParseFiniteDouble(property, value);

            if (result <= 0)
            {
                throw new FormatException(
                    $"{property} must be greater than zero.");
            }

            return result;
        }

        private static double ParseDimension(
            string property,
            string value)
        {
            if (string.Equals(
                value,
                "Auto",
                StringComparison.OrdinalIgnoreCase))
            {
                return double.NaN;
            }

            double result = ParseFiniteDouble(property, value);

            if (result < 0)
            {
                throw new FormatException(
                    $"{property} must be Auto or a non-negative number.");
            }

            return result;
        }

        private static Visibility ParseVisibility(string value)
        {
            if (!Enum.TryParse(
                value,
                true,
                out Visibility result) ||
                !Enum.IsDefined(typeof(Visibility), result))
            {
                throw new FormatException(
                    "Visibility must be Visible, Hidden or Collapsed.");
            }

            return result;
        }

        private static int ParseInteger(
            string property,
            string value)
        {
            if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result))
            {
                throw new FormatException(
                    $"{property} must contain a valid integer.");
            }

            return result;
        }

        private static string FormatNumber(double value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }

        private static string FormatDimension(double value)
        {
            return double.IsNaN(value)
                ? "Auto"
                : FormatNumber(value);
        }
    }
}