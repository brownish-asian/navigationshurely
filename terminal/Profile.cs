using navigationshurely.models;
using System;

namespace navigationshurely.terminal
{
    public class Profile : StorageFacility
    {
        public Profile(string name) : this(name, true)
        {
        }

        internal Profile(string name, bool initialiseConfigurations)
            : base(name)
        {
            if (!initialiseConfigurations)
                return;

            foreach (Controls control in Enum.GetValues(typeof(Controls)))
            {
                AddData(new Config(control));
            }
        }
    }
}