using navigationshurely.models;
using System;
using System.Globalization;

namespace navigationshurely.terminal
{
    public class Config : Data
    {
        public Controls Identifier { get; }

        public int Top { get; set; }

        public int Left { get; set; }

        public int Size { get; set; }

        public Config(Controls id) : base(id + ".config")
        {
            Identifier = id;
            _title = id.ToString();
        }

        public override string Info
        {
            get
            {
                return
                    $"Control={Identifier}\n" +
                    $"Top={Top}\n" +
                    $"Left={Left}\n" +
                    $"Size={Size}";
            }
        }

        public void ChangeInfo(string info)
        {
            ArgumentNullException.ThrowIfNull(info);

            int newTop = Top;
            int newLeft = Left;
            int newSize = Size;

            if (string.IsNullOrWhiteSpace(info))
            {
                Top = 0;
                Left = 0;
                Size = 0;
                return;
            }

            string[] lines = info.Replace("\r\n", "\n").Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                int separator = line.IndexOf('=');

                if (separator <= 0)
                    throw new FormatException($"Invalid configuration line: {line}");

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
                        newTop = ParseInteger(key, value);
                        break;

                    case "left":
                        newLeft = ParseInteger(key, value);
                        break;

                    case "size":
                        newSize = ParseInteger(key, value);
                        break;

                    default:
                        throw new FormatException(
                            $"Unknown configuration property: {key}");
                }
            }

            Top = newTop;
            Left = newLeft;
            Size = newSize;
        }

        public void SetValue(string property, string value)
        {
            int parsedValue = ParseInteger(property, value);

            switch (property.ToLowerInvariant())
            {
                case "top":
                    Top = parsedValue;
                    break;

                case "left":
                    Left = parsedValue;
                    break;

                case "size":
                    Size = parsedValue;
                    break;

                default:
                    throw new ArgumentException(
                        $"Unknown configuration property: {property}");
            }
        }

        private static int ParseInteger(string property, string value)
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
    }
}