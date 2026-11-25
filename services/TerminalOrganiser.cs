using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace navigationshurely.services
{
    public class TerminalOrganiser
    {
        // Actual command history.
        private readonly List<string> prevCommands = new();

        // Everything currently displayed in the terminal.
        public ObservableCollection<string> Transcript { get; }
            = new();

        // Expose history without allowing the input handler
        // to modify the underlying collection.
        public IReadOnlyList<string> History => prevCommands;

        public TerminalOrganiser() { }

        public void AddCommand(string command)
        {
            prevCommands.Add(command);

            Transcript.Add($"> {command}");
        }

        public void AddResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return;

            string[] lines = response
                .Replace("\r\n", "\n")
                .Split('\n');

            foreach (string line in lines)
            {
                Transcript.Add(line);
            }
        }

        public void ClearScreen()
        {
            Transcript.Clear();
        }

        public string GetAllCommands()
        {
            return string.Join(
                Environment.NewLine,
                prevCommands.ConvertAll(
                    command => $"> {command}"));
        }
    }
}