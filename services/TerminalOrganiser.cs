using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace navigationshurely.services
{
    public class TerminalOrganiser
    {
        public TerminalOrganiser() { }


        private List<string> prevCommands = new List<string>();

        public string GetAllCommands()
        {
            var stringBuilder = new StringBuilder();
            for (int i = 0; i < prevCommands.Count; i++)
            {
                stringBuilder.AppendLine($"> {prevCommands[i]}");
            }
            return stringBuilder.ToString();
        }

        public void AddCommand(string command)
        {
            prevCommands.Add(command);
        }
    }
}
