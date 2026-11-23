using navigationshurely.services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace navigationshurely
{
    /// <summary>
    /// Interaction logic for AdminWindow.xaml
    /// </summary>
    public partial class AdminWindow : Window
    {
        private readonly AdminInputHandler _inputHandler = new();
        private readonly TerminalOrganiser _organiser;
        private readonly Tokeniser _tokeniser = new();
        private readonly Processor processor = new();

        public AdminWindow()
        {
            InitializeComponent();
            UpdateCommandLine();
            AdminInputDisplay.Focus();
            _inputHandler.PassTheMessageToSession += SendOffToZsh;
        }

        private void UpdateCommandLine()
        {
            AdminInputDisplay.Text = _inputHandler.DisplayString();
            AdminInputDisplay.CaretIndex = _inputHandler.CursorIndex();
            AdminInputDisplay.Focus();
        }

        private void AdminInputDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            _inputHandler.ProcessKey(e);
            UpdateCommandLine();
        }

        private void AdminInputDisplay_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {

        }

        private void AdminScreen_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width;
            Top = workArea.Top + (workArea.Height - Height) / 2;


            DoubleAnimation scale = new DoubleAnimation(
                0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut,
                }
            };
            PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);

        }

        private void SendOffToZsh()
        {
            string userCommand = _inputHandler.CommandString();

            _organiser.AddCommand(userCommand);
            List<Token> command = _tokeniser.ParseToken(userCommand);

            if (command != null)
            {
                processor.ProcessCommand(command);
                string response = processor.Response();
                //display the response somehow
            }
        }
    }
}
