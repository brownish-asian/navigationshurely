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
        }

        public virtual string Name => _name;

        public virtual string Title => _name;

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
        }

        protected static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A name cannot be empty.");

            if (name is "." or "..")
                throw new ArgumentException("'.' and '..' are reserved directory names.");

            if (name.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0)
                throw new ArgumentException("A name cannot contain directory separators.");
        }
    }
}