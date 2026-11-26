using navigationshurely.terminal;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace navigationshurely.data
{
    public class KingState : INotifyPropertyChanged
    {
        private readonly Librarian _librarian;

        public event PropertyChangedEventHandler? PropertyChanged;

        private Visibility _testControlsVisibility =
            Visibility.Visible;

        public Profile ActiveProfile =>
            _librarian.CurrentProfile;

        public Visibility TestControlsVisibility
        {
            get => _testControlsVisibility;

            set
            {
                if (_testControlsVisibility == value)
                    return;

                _testControlsVisibility = value;

                OnPropertyChanged();
            }
        }

        public KingState(Librarian librarian)
        {
            _librarian = librarian ??
                throw new ArgumentNullException(nameof(librarian));

            _librarian.PropertyChanged +=
                Librarian_PropertyChanged;
        }

        private void Librarian_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(Librarian.CurrentProfile))
            {
                OnPropertyChanged(nameof(ActiveProfile));
            }
        }

        protected virtual void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}