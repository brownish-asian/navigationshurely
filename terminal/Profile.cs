using navigationshurely.models;
using System;

namespace navigationshurely.terminal
{
    public class Profile : StorageFacility
    {
        public Profile(string name) : this(name, true)
        {
        }

        internal Profile(
            string name,
            bool initialiseConfigurations)
            : base(name)
        {
            if (!initialiseConfigurations)
                return;

            foreach (Controls control in
                Enum.GetValues(typeof(Controls)))
            {
                AddData(new Config(control));
            }
        }

        public Config GetControlConfig(Controls control)
        {
            if (!Enum.IsDefined(typeof(Controls), control))
            {
                throw new ArgumentOutOfRangeException(nameof(control));
            }

            return AskForConfig(control.ToString())
                ?? throw new InvalidOperationException(
                    $"Profile '{Name}' has no configuration for '{control}'.");
        }

        public Config TitleBoxConfig =>
            GetControlConfig(Controls.TitleBox);

        public Config TranslationBoxConfig =>
            GetControlConfig(Controls.TranslationBox);
    }
}