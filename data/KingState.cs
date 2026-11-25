using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace navigationshurely.data
{
    public class KingState : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        private Visibility _testControlsVisibility;

        public Visibility TestControlsVisibility 
        {
            get => _testControlsVisibility;
            set
            {
                if (_testControlsVisibility == value)
                {
                    return;
                }

                _testControlsVisibility = value;
                OnPropertyChanged();
            }
        }


        //what else lol
        //positions of main window ui controls, then later on animations, but then like, do i bind it to the actual controls?
        //and thus, i have a constructor for KingState that takes in classes of information about the positions and animations
        //of the controls and those classes are saved by plumber?

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
