using System;

namespace navigationshurely.terminal
{
    public class TextFile : Data
    {
        public string Content { get; private set; }

        public TextFile(string name, string content = "")
            : base(name)
        {
            SetFileName(name);
            Content = content ?? string.Empty;
        }
        public override void Rename(string newName)
        {
            SetFileName(newName);
        }

        public override string Info => Content;

        public void Write(string content, bool append = false)
        {
            ArgumentNullException.ThrowIfNull(content);

            Content = append
                ? Content + content
                : content;
        }
    }
}