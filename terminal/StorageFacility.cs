using System;
using System.Collections.Generic;
using System.Linq;

namespace navigationshurely.terminal
{
    public class StorageFacility : Data
    {
        protected readonly List<Data> data;

        public IReadOnlyList<Data> Children => data;

        public override string Title => Name;

        public StorageFacility(string name) : base(name)
        {
            data = new List<Data>();
        }

        public override string Info
        {
            get
            {
                return string.Join(
                    Environment.NewLine,
                    data.Select(item => item.Title));
            }
        }

        public void AddData(Data item)
        {
            ArgumentNullException.ThrowIfNull(item);

            ValidateName(item.Name);

            // Prevent a directory from becoming its own descendant.
            StorageFacility? ancestor = this;

            while (ancestor != null)
            {
                if (ReferenceEquals(ancestor, item))
                    throw new InvalidOperationException(
                        "A directory cannot be placed inside itself.");

                ancestor = ancestor.GetOwner() as StorageFacility;
            }

            Data? existing = GetData(item.Name);

            if (ReferenceEquals(existing, item))
                return;

            if (existing != null)
                throw new InvalidOperationException(
                    $"An item named '{item.Name}' already exists.");

            // Detach the item from its previous directory.
            if (item.GetOwner() is StorageFacility previousOwner)
            {
                previousOwner.RemoveData(item);
            }

            data.Add(item);
            item.SetOwner(this);
        }

        public bool RemoveData(Data item)
        {
            ArgumentNullException.ThrowIfNull(item);

            int index = data.FindIndex(
                existing => ReferenceEquals(existing, item));

            if (index < 0)
                return false;

            data.RemoveAt(index);

            if (ReferenceEquals(item.GetOwner(), this))
                item.SetOwner(null);

            return true;
        }

        public bool RemoveData(string name)
        {
            Data? item = GetData(name);

            return item != null && RemoveData(item);
        }

        public Data? GetData(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return data.FirstOrDefault(
                item => string.Equals(
                    item.Name,
                    name,
                    StringComparison.Ordinal));
        }

        public Config? AskForConfig(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            string expectedName = title.EndsWith(
                ".config",
                StringComparison.OrdinalIgnoreCase)
                    ? title
                    : title + ".config";

            return data
                .OfType<Config>()
                .FirstOrDefault(config =>
                    string.Equals(config.Name, title,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(config.Name, expectedName,
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}