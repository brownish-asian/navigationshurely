using navigationshurely.models;
using navigationshurely.terminal;
using System;

namespace navigationshurely.services
{
    public sealed class ShiftManager
    {
        private readonly Librarian _librarian;

        public ShiftManager(Librarian librarian)
        {
            _librarian = librarian ??
                throw new ArgumentNullException(nameof(librarian));
        }

        // ----------------------------------------------------
        // ABSOLUTE POSITIONING
        // ----------------------------------------------------

        public void SetPosition(
            Controls control,
            double? left,
            double? top)
        {
            if (!left.HasValue && !top.HasValue)
            {
                throw new ArgumentException(
                    "At least one coordinate must be supplied.");
            }

            Config config = GetActiveConfig(control);

            config.SetPosition(left, top);
        }

        // ----------------------------------------------------
        // RELATIVE POSITIONING
        // ----------------------------------------------------

        public void Shift(
            Controls control,
            double? deltaX,
            double? deltaY)
        {
            if (!deltaX.HasValue && !deltaY.HasValue)
            {
                throw new ArgumentException(
                    "At least one shift value must be supplied.");
            }

            Config config = GetActiveConfig(control);

            // Calculate every requested coordinate before changing
            // the configuration. Config.SetPosition validates both
            // values before committing either one.

            double? newLeft = deltaX.HasValue
                ? config.Left + deltaX.Value
                : null;

            double? newTop = deltaY.HasValue
                ? config.Top + deltaY.Value
                : null;

            config.SetPosition(newLeft, newTop);
        }

        // ----------------------------------------------------
        // CONFIGURATION ACCESS
        // ----------------------------------------------------

        private Config GetActiveConfig(Controls control)
        {
            if (!Enum.IsDefined(typeof(Controls), control))
            {
                throw new ArgumentOutOfRangeException(nameof(control));
            }

            return _librarian.CurrentProfile
                .GetControlConfig(control);
        }
    }
}