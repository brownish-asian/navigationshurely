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

        public Config FrequencyBoxConfig =>
            GetControlConfig(Controls.FrequencyBox);

        public Config AliasBoxConfig =>
            GetControlConfig(Controls.AliasBox);

        public Config ModeBoxConfig =>
            GetControlConfig(Controls.ModeBox);

        public Config VerseBoxConfig =>
            GetControlConfig(Controls.VerseBox);

        public Config GuidedHudConfig =>
            GetControlConfig(Controls.GuidedHud);

        public Config RightListConfig =>
            GetControlConfig(Controls.RightList);

        public Config BottomListConfig =>
            GetControlConfig(Controls.BottomList);
    }
}