using System.Collections.Generic;

namespace navigationshurely.terminal
{
    /// <summary>Serializable representation of the in-memory terminal filesystem.</summary>
    public sealed class FileSystemSnapshot
    {
        public List<FileSystemEntrySnapshot> Entries { get; set; } = new();
        public string ActiveProfileName { get; set; } = "default_profile";
        public string WorkingDirectoryPath { get; set; } = string.Empty;
    }

    public sealed class FileSystemEntrySnapshot
    {
        // Values are: directory, profile, text, or config.
        public string Kind { get; set; } = string.Empty;
        // Directories use their Name; files use their complete Title/filename.
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ConfigIdentifier { get; set; } = string.Empty;
        public List<FileSystemEntrySnapshot> Children { get; set; } = new();
    }
}
