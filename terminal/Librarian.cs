using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using navigationshurely.models;

namespace navigationshurely.terminal
{
    public class Librarian : StorageFacility, INotifyPropertyChanged
    {
        private Profile currentProfile;
        private StorageFacility workingDirectory;

        public Profile CurrentProfile => currentProfile;

        public Librarian(string name) : base(NormalizeRootName(name))
        {
            currentProfile = new Profile("default_profile");

            AddData(currentProfile);

            workingDirectory = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }

        // ----------------------------------------------------
        // PERSISTENCE
        // ----------------------------------------------------

        public FileSystemSnapshot ExportFileSystem()
        {
            var snapshot = new FileSystemSnapshot
            {
                ActiveProfileName = currentProfile.Name,
                WorkingDirectoryPath = GetPath(workingDirectory)
            };

            foreach (Data child in Children)
                snapshot.Entries.Add(CreateSnapshot(child));

            return snapshot;
        }

        public void RestoreFileSystem(FileSystemSnapshot? snapshot)
        {
            if (snapshot == null)
                return;

            // Build and validate the replacement tree before touching the live one.
            var stagingRoot = new StorageFacility(Name);
            foreach (FileSystemEntrySnapshot entry in snapshot.Entries ?? new())
                stagingRoot.AddData(RestoreEntry(entry));

            Profile? restoredProfile = stagingRoot.Children
                .OfType<Profile>()
                .FirstOrDefault(profile => string.Equals(
                    profile.Name, snapshot.ActiveProfileName, StringComparison.Ordinal));

            if (restoredProfile == null)
            {
                restoredProfile = stagingRoot.Children.OfType<Profile>()
                    .FirstOrDefault();
            }

            if (restoredProfile == null)
            {
                restoredProfile = new Profile("default_profile");
                stagingRoot.AddData(restoredProfile);
            }

            foreach (Profile profile in stagingRoot.Children.OfType<Profile>())
                EnsureProfileConfigurations(profile);

            foreach (Data existing in Children.ToList())
                RemoveData(existing);

            foreach (Data restored in stagingRoot.Children.ToList())
                AddData(restored);

            currentProfile = Children.OfType<Profile>()
                .First(profile => string.Equals(
                    profile.Name, restoredProfile.Name, StringComparison.Ordinal));

            // Resolve again now that the restored objects belong to this root.
            workingDirectory = currentProfile;
            if (!string.IsNullOrWhiteSpace(snapshot.WorkingDirectoryPath) &&
                TryResolve(snapshot.WorkingDirectoryPath, out Data? liveWorking, out _) &&
                liveWorking is StorageFacility liveDirectory)
            {
                workingDirectory = liveDirectory;
            }

            OnPropertyChanged(nameof(CurrentProfile));
        }

        private static FileSystemEntrySnapshot CreateSnapshot(Data item)
        {
            var entry = new FileSystemEntrySnapshot();

            switch (item)
            {
                case Profile profile:
                    entry.Kind = "profile";
                    entry.Name = profile.Name;
                    foreach (Data child in profile.Children)
                        entry.Children.Add(CreateSnapshot(child));
                    break;

                case StorageFacility directory:
                    entry.Kind = "directory";
                    entry.Name = directory.Name;
                    foreach (Data child in directory.Children)
                        entry.Children.Add(CreateSnapshot(child));
                    break;

                case Config config:
                    entry.Kind = "config";
                    entry.Name = config.Title;
                    entry.ConfigIdentifier = config.Identifier.ToString();
                    entry.Content = config.Info;
                    break;

                case TextFile file:
                    entry.Kind = "text";
                    entry.Name = file.Title;
                    entry.Content = file.Content;
                    break;

                default:
                    entry.Kind = "text";
                    entry.Name = item.Title;
                    entry.Content = item.Info;
                    break;
            }

            return entry;
        }

        private static Data RestoreEntry(FileSystemEntrySnapshot entry)
        {
            if (entry == null)
                throw new InvalidOperationException("The saved filesystem contains an empty entry.");

            switch (entry.Kind)
            {
                case "profile":
                case "directory":
                    StorageFacility directory = entry.Kind == "profile"
                        ? new Profile(entry.Name, false)
                        : new StorageFacility(entry.Name);

                    foreach (FileSystemEntrySnapshot child in entry.Children ?? new())
                        directory.AddData(RestoreEntry(child));

                    return directory;

                case "config":
                    if (!Enum.TryParse(entry.ConfigIdentifier, true, out Controls identifier) ||
                        !Enum.IsDefined(typeof(Controls), identifier))
                    {
                        throw new InvalidOperationException(
                            $"Unknown saved control configuration '{entry.ConfigIdentifier}'.");
                    }

                    var config = new Config(identifier);
                    config.ChangeInfo(entry.Content ?? string.Empty);
                    if (!string.Equals(config.Title, entry.Name, StringComparison.Ordinal))
                        config.Rename(entry.Name);
                    return config;

                case "text":
                    return new TextFile(entry.Name, entry.Content ?? string.Empty);

                default:
                    throw new InvalidOperationException(
                        $"Unknown saved filesystem entry type '{entry.Kind}'.");
            }
        }

        private static void EnsureProfileConfigurations(Profile profile)
        {
            foreach (Controls control in Enum.GetValues(typeof(Controls)))
            {
                if (profile.AskForConfig(control.ToString()) == null)
                    profile.AddData(new Config(control));
            }
        }

        // ----------------------------------------------------
        // DIRECTORY INFORMATION
        // ----------------------------------------------------

        public string WorkingDirectory()
        {
            return GetPath(workingDirectory);
        }

        public string List()
        {
            return List(".", false, false);
        }

        public string List(
            string? path,
            bool showHidden,
            bool longFormat)
        {
            if (!TryResolve(path ?? ".", out Data? item, out string error))
                return error;

            if (item is not StorageFacility directory)
                return item.Title;

            IEnumerable<Data> entries = directory.Children;

            if (!showHidden)
            {
                entries = entries.Where(
                    entry => !entry.Name.StartsWith(".", StringComparison.Ordinal));
            }

            if (longFormat)
            {
                return string.Join(
                    Environment.NewLine,
                    entries.Select(entry =>
                    {
                        string type = entry is StorageFacility ? "d" : "-";

                        int size = entry is Config config
                            ? config.Info.Length
                            : entry is TextFile file
                                ? file.Content.Length
                                : 0;

                        return $"{type} {size,8} {GetStorageKey(entry)}";
                    }));
            }

            return string.Join(
                Environment.NewLine,
                entries.Select(entry => entry.Title));
        }

        public string Stat(string path)
        {
            if (!TryResolve(path, out Data? item, out string error))
                return error;

            string type = item switch
            {
                Profile => "profile",
                StorageFacility => "directory",
                Config => "configuration file",
                TextFile => "text file",
                _ => "file"
            };

            int size = item is Config config
                ? config.Info.Length
                : item is TextFile file
                    ? file.Content.Length
                    : 0;

            return
                $"Name: {item.Name}\n" +
                $"Title: {item.Title}\n" +
                $"Type: {type}\n" +
                $"Path: {GetPath(item)}\n" +
                $"Size: {size} bytes";
        }

        // ----------------------------------------------------
        // DIRECTORY NAVIGATION
        // ----------------------------------------------------

        public string ChangeDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = "~";

            if (!TryResolve(path, out Data? item, out string error))
                return error;

            if (item is not StorageFacility directory)
                return $"cd: '{path}' is not a directory.";

            workingDirectory = directory;
            return string.Empty;
        }


        // ----------------------------------------------------
        // PROFILE MANAGEMENT
        // ----------------------------------------------------

        public string ListProfiles()
        {
            return string.Join(
                Environment.NewLine,
                Children.OfType<Profile>()
                    .Select(profile =>
                        profile == currentProfile
                            ? $"{profile.Name} (active)"
                            : profile.Name));
        }

        public string CreateProfile(string name)
        {
            try
            {
                ValidateName(name);

                if (GetData(name) != null)
                    return $"A profile or directory named '{name}' already exists.";

                AddData(new Profile(name));

                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"mkprofile: {ex.Message}";
            }
        }

        public string SelectProfile(string name)
        {
            Data? item = GetData(name);

            if (item is not Profile profile)
                return $"useprofile: Profile '{name}' does not exist.";

            bool profileChanged =
                !ReferenceEquals(currentProfile, profile);

            currentProfile = profile;
            workingDirectory = profile;

            if (profileChanged)
                OnPropertyChanged(nameof(CurrentProfile));

            return string.Empty;
        }

        // ----------------------------------------------------
        // DIRECTORY CREATION
        // ----------------------------------------------------

        public string CreateDirectory(string path, bool parents = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "mkdir: missing directory name.";

            if (parents)
                return CreateDirectoryTree(path);

            if (!TryResolveParent(
                path,
                out StorageFacility? parent,
                out string name,
                out string error))
            {
                return $"mkdir: {error}";
            }

            if (parent.GetData(name) != null)
                return $"mkdir: '{path}' already exists.";

            try
            {
                parent.AddData(new StorageFacility(name));
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"mkdir: {ex.Message}";
            }
        }

        private string CreateDirectoryTree(string path)
        {
            if (!TryGetStartingDirectory(
                ref path,
                out StorageFacility? current,
                out string error))
            {
                return $"mkdir: {error}";
            }

            string[] parts = path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                if (part == ".")
                    continue;

                if (part == "..")
                {
                    current = current.GetOwner() as StorageFacility ?? this;
                    continue;
                }

                Data? existing = current.GetData(part);

                if (existing is StorageFacility existingDirectory)
                {
                    current = existingDirectory;
                    continue;
                }

                if (existing != null)
                    return $"mkdir: '{part}' is not a directory.";

                try
                {
                    StorageFacility newDirectory = new StorageFacility(part);
                    current.AddData(newDirectory);
                    current = newDirectory;
                }
                catch (Exception ex)
                {
                    return $"mkdir: {ex.Message}";
                }
            }

            return string.Empty;
        }

        // ----------------------------------------------------
        // FILE CREATION
        // ----------------------------------------------------

        public string Touch(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "touch: missing file name.";

            // Existing files and directories need no further action.
            if (TryResolve(path, out _, out _))
                return string.Empty;

            if (!TryResolveParent(
                path,
                out StorageFacility? parent,
                out string name,
                out string error))
            {
                return $"touch: {error}";
            }

            try
            {
                parent.AddData(new TextFile(name));
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"touch: {ex.Message}";
            }
        }

        // ----------------------------------------------------
        // FILE READING AND WRITING
        // ----------------------------------------------------

        public bool TryReadFile(
            string path,
            out string content,
            out string error)
        {
            content = string.Empty;
            error = string.Empty;

            if (!TryResolve(path, out Data? item, out error))
                return false;

            if (item is StorageFacility)
            {
                error = $"cat: '{path}' is a directory.";
                return false;
            }

            if (item is Config config)
            {
                content = config.Info;
                return true;
            }

            if (item is TextFile file)
            {
                content = file.Content;
                return true;
            }

            content = item.Info;
            return true;
        }

        public bool TryOpenForEditing(
    string path,
    out string content,
    out string error)
        {
            content = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "vim: missing filename.";
                return false;
            }

            // First, determine whether the requested item already exists.
            if (TryResolve(path, out Data? item, out _))
            {
                if (item is StorageFacility)
                {
                    error = $"vim: '{path}' is a directory.";
                    return false;
                }

                if (item is TextFile file)
                {
                    content = file.Content;
                    return true;
                }

                if (item is Config config)
                {
                    content = config.Info;
                    return true;
                }

                error = $"vim: '{path}' is not an editable file.";
                return false;
            }

            // The file does not exist. Check that its parent directory
            // exists and that the requested filename is valid.
            if (!TryResolveParent(
                path,
                out StorageFacility? parent,
                out string name,
                out string parentError))
            {
                error = $"vim: {parentError}";
                return false;
            }

            if (parent!.GetData(name) != null)
            {
                error = $"vim: '{path}' cannot be opened for editing.";
                return false;
            }

            // New files begin as temporary empty buffers.
            // Nothing is inserted into the filesystem until :w.
            content = string.Empty;

            return true;
        }

        public string ExposeConfig(string name)
        {
            return TryReadFile(name, out string content, out string error)
                ? content
                : error;
        }

        public string WriteFile(
            string path,
            string content,
            bool append = false)
        {
            ArgumentNullException.ThrowIfNull(content);

            if (TryResolve(path, out Data? existing, out _))
            {
                try
                {
                    if (existing is Config config)
                    {
                        string newContent = append
                            ? config.Info + "\n" + content
                            : content;

                        config.ChangeInfo(newContent);
                        return string.Empty;
                    }

                    if (existing is TextFile file)
                    {
                        file.Write(content, append);
                        return string.Empty;
                    }

                    return $"'{path}' is not a writable file.";
                }
                catch (Exception ex)
                {
                    return $"write: {ex.Message}";
                }
            }

            if (!TryResolveParent(
                path,
                out StorageFacility? parent,
                out string name,
                out string error))
            {
                return $"write: {error}";
            }

            try
            {
                parent.AddData(new TextFile(name, content));
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"write: {ex.Message}";
            }
        }

        // ----------------------------------------------------
        // FILE AND DIRECTORY REMOVAL
        // ----------------------------------------------------

        public string Remove(
            string path,
            bool recursive = false,
            bool force = false)
        {
            if (!TryResolve(path, out Data? item, out string error))
            {
                return force ? string.Empty : $"rm: {error}";
            }

            if (ReferenceEquals(item, this))
                return "rm: cannot remove the filesystem root.";

            if (item is Profile profile &&
                ReferenceEquals(profile, currentProfile))
            {
                return "rm: cannot remove the active profile.";
            }

            if (item is StorageFacility directory &&
                IsWithin(workingDirectory, directory))
            {
                return "rm: cannot remove the current directory or one of its parents.";
            }

            if (item is StorageFacility && !recursive)
                return $"rm: '{path}' is a directory. Use -r to remove it.";

            if (item.GetOwner() is not StorageFacility parent)
                return "rm: cannot find the containing directory.";

            parent.RemoveData(item);

            return string.Empty;
        }

        public string RemoveDirectory(string path)
        {
            if (!TryResolve(path, out Data? item, out string error))
                return $"rmdir: {error}";

            if (ReferenceEquals(item, this))
                return "rmdir: cannot remove the filesystem root.";

            if (item is not StorageFacility directory)
                return $"rmdir: '{path}' is not a directory.";

            if (directory.Children.Count > 0)
                return $"rmdir: '{path}' is not empty.";

            if (directory is Profile profile &&
                ReferenceEquals(profile, currentProfile))
            {
                return "rmdir: cannot remove the active profile.";
            }

            if (IsWithin(workingDirectory, directory))
                return "rmdir: cannot remove the current directory or one of its parents.";

            if (directory.GetOwner() is StorageFacility parent)
                parent.RemoveData(directory);

            return string.Empty;
        }

        // ----------------------------------------------------
        // COPYING
        // ----------------------------------------------------

        public string Copy(
            string sourcePath,
            string destinationPath,
            bool recursive = false)
        {
            if (!TryResolve(sourcePath, out Data? source, out string error))
                return $"cp: {error}";

            if (ReferenceEquals(source, this))
                return "cp: cannot copy the filesystem root.";

            if (source is StorageFacility sourceDirectory)
            {
                if (!recursive)
                    return "cp: omitting directory. Use -r to copy directories.";

                if (!TryGetDestination(
                    source,
                    destinationPath,
                    out StorageFacility? destination,
                    out string name,
                    out error))
                {
                    return $"cp: {error}";
                }

                if (IsWithin(destination, sourceDirectory))
                    return "cp: cannot copy a directory into itself.";

                if (destination.GetData(name) != null)
                    return $"cp: destination '{name}' already exists.";

                try
                {
                    destination.AddData(CloneData(source, name));
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    return $"cp: {ex.Message}";
                }
            }

            if (!TryGetDestination(
                source,
                destinationPath,
                out StorageFacility? parent,
                out string destinationName,
                out error))
            {
                return $"cp: {error}";
            }

            if (parent.GetData(destinationName) != null)
                return $"cp: destination '{destinationName}' already exists.";

            try
            {
                parent.AddData(CloneData(source, destinationName));
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"cp: {ex.Message}";
            }
        }

        private static Data CloneData(Data source, string newName)
        {
            switch (source)
            {
                case Config config:
                    return new Config(config.Identifier)
                    {
                        Top = config.Top,
                        Left = config.Left,
                        Width = config.Width,
                        Height = config.Height,
                        FontSize = config.FontSize,
                        ContentScale = config.ContentScale,
                        Visibility = config.Visibility,
                        Size = config.Size
                    };

                case TextFile file:
                    return new TextFile(newName, file.Content);

                case Profile profile:
                    {
                        var copy = new Profile(newName, false);

                        foreach (Data child in profile.Children)
                            copy.AddData(CloneData(child, GetStorageKey(child)));

                        return copy;
                    }

                case StorageFacility directory:
                    {
                        var copy = new StorageFacility(newName);

                        foreach (Data child in directory.Children)
                            copy.AddData(CloneData(child, GetStorageKey(child)));

                        return copy;
                    }

                default:
                    return new TextFile(newName, source.Info);
            }
        }

        // ----------------------------------------------------
        // MOVING AND RENAMING
        // ----------------------------------------------------

        public string Move(string sourcePath, string destinationPath)
        {
            if (!TryResolve(sourcePath, out Data? source, out string error))
                return $"mv: {error}";

            if (ReferenceEquals(source, this))
                return "mv: cannot move the filesystem root.";

            if (TryResolve(destinationPath, out Data? sameItem, out _) &&
                ReferenceEquals(source, sameItem))
            {
                return string.Empty;
            }

            if (!TryGetDestination(
                source,
                destinationPath,
                out StorageFacility? destination,
                out string newName,
                out error))
            {
                return $"mv: {error}";
            }

            Data? collision = destination.GetData(newName);

            if (collision != null && !ReferenceEquals(collision, source))
                return $"mv: destination '{newName}' already exists.";

            if (source is StorageFacility sourceDirectory &&
                IsWithin(workingDirectory, sourceDirectory))
            {
                return "mv: cannot move the current directory or one of its parents.";
            }

            if (source is StorageFacility directory &&
                IsWithin(destination, directory))
            {
                return "mv: cannot move a directory into itself.";
            }

            if (source.GetOwner() is not StorageFacility previousParent)
                return "mv: cannot find the original directory.";

            string oldName = GetStorageKey(source);

            try
            {
                previousParent.RemoveData(source);
                source.Rename(newName);
                destination.AddData(source);

                return string.Empty;
            }
            catch (Exception ex)
            {
                // Attempt to restore the original item.
                try
                {
                    source.Rename(oldName);
                    previousParent.AddData(source);
                }
                catch
                {
                    // Preserve the original error.
                }

                return $"mv: {ex.Message}";
            }
        }

        // ----------------------------------------------------
        // DIRECTORY TREES
        // ----------------------------------------------------

        public string Tree(string? path = null)
        {
            if (!TryResolve(path ?? ".", out Data? item, out string error))
                return $"tree: {error}";

            var builder = new StringBuilder();

            builder.AppendLine(GetStorageKey(item));

            if (item is StorageFacility directory)
                AppendTree(builder, directory, 1);

            return builder.ToString().TrimEnd();
        }

        private static void AppendTree(
            StringBuilder builder,
            StorageFacility directory,
            int depth)
        {
            foreach (Data child in directory.Children)
            {
                builder.Append(' ', depth * 4);

                if (child is StorageFacility)
                    builder.AppendLine(child.Name + "/");
                else
                    builder.AppendLine(GetStorageKey(child));

                if (child is StorageFacility childDirectory)
                    AppendTree(builder, childDirectory, depth + 1);
            }
        }

        // ----------------------------------------------------
        // SEARCHING
        // ----------------------------------------------------

        public string Find(string path = ".", string pattern = "*")
        {
            if (!TryResolve(path, out Data? start, out string error))
                return $"find: {error}";

            var results = new List<string>();

            FindRecursive(start, pattern, results);

            return string.Join(Environment.NewLine, results);
        }

        private void FindRecursive(
            Data item,
            string pattern,
            List<string> results)
        {
            if (WildcardMatches(pattern, item.Name) ||
                WildcardMatches(pattern, item.Title))
            {
                results.Add(GetPath(item));
            }

            if (item is not StorageFacility directory)
                return;

            foreach (Data child in directory.Children)
                FindRecursive(child, pattern, results);
        }

        private static bool WildcardMatches(string pattern, string value)
        {
            string expression =
                "^" +
                Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") +
                "$";

            return Regex.IsMatch(
                value,
                expression,
                RegexOptions.CultureInvariant);
        }

        // ----------------------------------------------------
        // PATH RESOLUTION
        // ----------------------------------------------------

        private bool TryResolve(
            string path,
            out Data? result,
            out string error)
        {
            result = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "missing path.";
                return false;
            }

            if (!TryGetStartingDirectory(
                ref path,
                out StorageFacility? current,
                out error))
            {
                return false;
            }

            string[] parts = path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

            Data cursor = current;

            foreach (string part in parts)
            {
                if (cursor is not StorageFacility directory)
                {
                    error = $"'{cursor.Name}' is not a directory.";
                    return false;
                }

                if (part == ".")
                    continue;

                if (part == "..")
                {
                    cursor = directory.GetOwner() as StorageFacility ?? this;
                    continue;
                }

                Data? next = directory.GetData(part);

                if (next == null)
                {
                    error = $"No such file or directory: '{part}'.";
                    return false;
                }

                cursor = next;
            }

            result = cursor;
            return true;
        }

        private bool TryGetStartingDirectory(
            ref string path,
            out StorageFacility? start,
            out string error)
        {
            error = string.Empty;
            start = null;

            path = path.Replace('\\', '/');
            string rootPath = Title;

            // The filesystem root itself: /home
            if (string.Equals(
                path,
                rootPath,
                StringComparison.Ordinal))
            {
                start = this;
                path = string.Empty;
                return true;
            }

            // A path beneath the root: /home/something
            string rootPrefix = rootPath + "/";

            if (path.StartsWith(
                rootPrefix,
                StringComparison.Ordinal))
            {
                start = this;
                path = path[rootPrefix.Length..];
                return true;
            }

            if (path == "~")
            {
                start = currentProfile;
                path = string.Empty;
                return true;
            }

            if (path.StartsWith("~/", StringComparison.Ordinal))
            {
                start = currentProfile;
                path = path[2..];
                return true;
            }

            if (path.StartsWith("~", StringComparison.Ordinal))
            {
                error = $"Unknown home directory: '{path}'.";
                return false;
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                start = this;
                path = path.TrimStart('/');
                return true;
            }

            start = workingDirectory;
            return true;
        }

        private bool TryResolveParent(
            string path,
            out StorageFacility? parent,
            out string name,
            out string error)
        {
            parent = null;
            name = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "missing path.";
                return false;
            }

            string normalized = path
                .Replace('\\', '/')
                .TrimEnd('/');

            if (normalized.Length == 0)
            {
                error = "cannot use the filesystem root as a filename.";
                return false;
            }

            int separator = normalized.LastIndexOf('/');

            name = separator < 0
                ? normalized
                : normalized[(separator + 1)..];

            string parentPath = separator < 0
                ? "."
                : separator == 0
                    ? "/"
                    : normalized[..separator];

            if (name is "." or ".." || string.IsNullOrWhiteSpace(name))
            {
                error = "invalid filename.";
                return false;
            }

            if (!TryResolve(parentPath, out Data? parentData, out error))
                return false;

            if (parentData is not StorageFacility directory)
            {
                error = $"'{parentPath}' is not a directory.";
                return false;
            }

            parent = directory;
            return true;
        }

        private bool TryGetDestination(
            Data source,
            string destinationPath,
            out StorageFacility? destination,
            out string name,
            out string error)
        {
            destination = null;
            name = string.Empty;
            error = string.Empty;

            bool trailingSlash =
                destinationPath.EndsWith("/", StringComparison.Ordinal) ||
                destinationPath.EndsWith("\\", StringComparison.Ordinal);

            if (TryResolve(destinationPath, out Data? existing, out _))
            {
                if (existing is not StorageFacility directory)
                {
                    error = $"destination '{destinationPath}' already exists.";
                    return false;
                }

                destination = directory;
                name = GetStorageKey(source);
                return true;
            }

            if (trailingSlash)
            {
                error = $"destination directory '{destinationPath}' does not exist.";
                return false;
            }

            if (!TryResolveParent(
                destinationPath,
                out destination,
                out name,
                out error))
            {
                return false;
            }

            return true;
        }

        private string GetPath(Data item)
        {
            string rootPath = Title;

            if (ReferenceEquals(item, this))
                return rootPath;

            var parts = new Stack<string>();

            Data? cursor = item;

            while (cursor != null && !ReferenceEquals(cursor, this))
            {
                parts.Push(GetStorageKey(cursor));
                cursor = cursor.GetOwner();
            }

            return rootPath + "/" + string.Join("/", parts);
        }

        private static bool IsWithin(
            StorageFacility directory,
            StorageFacility possibleAncestor)
        {
            StorageFacility? cursor = directory;

            while (cursor != null)
            {
                if (ReferenceEquals(cursor, possibleAncestor))
                    return true;

                cursor = cursor.GetOwner() as StorageFacility;
            }

            return false;
        }

        private static string NormalizeRootName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Root name cannot be empty.");

            string normalized = name
                .Replace('\\', '/')
                .Trim('/');

            if (normalized.Length == 0 || normalized.Contains('/'))
            {
                throw new ArgumentException(
                    "The root name must be a single directory name.");
            }

            return normalized;
        }


        // ----------------------------------------------------
        // TAB COMPLETION: PATHS
        // ----------------------------------------------------

        public IReadOnlyList<string> CompletePaths(string pathPrefix)
        {
            pathPrefix ??= string.Empty;

            string normalized = pathPrefix.Replace('\\', '/');

            // Typing ~ and pressing Tab completes the home directory.
            if (normalized == "~")
                return new[] { "~/" };

            string directoryPath;
            string directoryPrefix;
            string partialName;

            if (normalized.Length == 0)
            {
                directoryPath = ".";
                directoryPrefix = string.Empty;
                partialName = string.Empty;
            }
            else if (normalized.EndsWith("/", StringComparison.Ordinal))
            {
                directoryPath = normalized.TrimEnd('/');

                if (directoryPath.Length == 0)
                    directoryPath = "/";

                directoryPrefix = normalized;
                partialName = string.Empty;
            }
            else
            {
                int separator = normalized.LastIndexOf('/');

                if (separator < 0)
                {
                    directoryPath = ".";
                    directoryPrefix = string.Empty;
                    partialName = normalized;
                }
                else
                {
                    directoryPath = separator == 0
                        ? "/"
                        : normalized[..separator];

                    directoryPrefix = normalized[..(separator + 1)];
                    partialName = normalized[(separator + 1)..];
                }
            }

            if (!TryResolve(
                directoryPath,
                out Data? resolved,
                out _))
            {
                return Array.Empty<string>();
            }

            if (resolved is not StorageFacility directory)
                return Array.Empty<string>();

            var results = new List<string>();

            // Include relative directory navigation when the user
            // has started typing a dot.
            if (partialName.StartsWith(".", StringComparison.Ordinal))
            {
                if (".".StartsWith(
                    partialName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(directoryPrefix + "./");
                }

                if ("..".StartsWith(
                    partialName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(directoryPrefix + "../");
                }
            }

            foreach (Data child in directory.Children
                .OrderBy(
                    item => GetStorageKey(item),
                    StringComparer.OrdinalIgnoreCase))
            {
                string childName = GetStorageKey(child);

                if (!childName.StartsWith(
                    partialName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = directoryPrefix + childName;

                if (child is StorageFacility)
                    candidate += "/";

                results.Add(candidate);
            }

            return results;
        }

        // ----------------------------------------------------
        // TAB COMPLETION: PROFILES
        // ----------------------------------------------------

        public IReadOnlyList<string> CompleteProfiles(string prefix)
        {
            prefix ??= string.Empty;

            return Children
                .OfType<Profile>()
                .Where(profile =>
                    profile.Name.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    profile => profile.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Select(profile => profile.Name)
                .ToList();
        }
    }
}