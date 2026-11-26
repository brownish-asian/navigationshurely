using System;

namespace navigationshurely.terminal
{
    public class Data
    {
        protected string _name;
        protected string _info = string.Empty;
        protected string _title = string.Empty;
        protected Data? owner;

        public Data(string? name)
        {
            _name = name ?? string.Empty;
            _title = _name;
        }

        // Name is the extensionless identity of a file.
        public virtual string Name => _name;

        // Title is the complete filename shown to the user.
        public virtual string Title => _title;

        public virtual string Info => _info;

        public Data? GetOwner()
        {
            return owner;
        }

        public virtual void SetOwner(Data? newOwner)
        {
            owner = newOwner;
        }

        public virtual void Rename(string newName)
        {
            ValidateName(newName);
            _name = newName;
            _title = newName;
        }

        // File-like types call this with the complete filename.
        // Only the final extension is removed from Name.
        // Dotfiles such as .gitignore keep their leading dot.
        protected void SetFileName(string fileName)
        {
            ValidateName(fileName);

            int extensionSeparator = fileName.LastIndexOf('.');

            _name = extensionSeparator > 0 &&
                    extensionSeparator < fileName.Length - 1
                ? fileName[..extensionSeparator]
                : fileName;

            _title = fileName;
        }

        // Directories use Name as their storage key.
        // Files use Title so different extensions can coexist.
        protected static string GetStorageKey(Data item)
        {
            return item is StorageFacility ? item.Name : item.Title;
        }

        protected static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A name cannot be empty.");

            if (name is "." or "..")
                throw new ArgumentException(
                    "'.' and '..' are reserved directory names.");

            if (name.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0)
                throw new ArgumentException(
                    "A name cannot contain directory separators.");
        }
    }
}