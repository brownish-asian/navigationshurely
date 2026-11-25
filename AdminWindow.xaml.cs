using navigationshurely.data;
using navigationshurely.services;
using navigationshurely.terminal;
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
        private readonly TerminalOrganiser _organiser = new();
        private readonly Tokeniser _tokeniser = new();
        private readonly Processor processor;

        private KingState kingState;
        private Librarian librarian;

        public AdminWindow(KingState kingState, Librarian librarian)
        {
            InitializeComponent();
            TerminalOutput.ItemsSource = _organiser.Transcript;
            this.kingState = kingState;
            this.librarian = librarian;

            processor = new Processor(librarian, _organiser.GetAllCommands);

            TerminalCompleter completer = new TerminalCompleter(librarian);

            _inputHandler.HistoryProvider = () => _organiser.History;
            _inputHandler.CompletionProvider = completer.GetCompletions;

            processor.ExitTerminal += Exit;
            processor.ClearTerminal += _organiser.ClearScreen;

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
            string userCommand = _inputHandler.CommandString().Trim();

            if (string.IsNullOrWhiteSpace(userCommand))
            {
                _inputHandler.Clear();
                UpdateCommandLine();
                return;
            }

            _organiser.AddCommand(userCommand);

            try
            {
                List<Token> command = _tokeniser.ParseToken(userCommand);

                processor.ProcessCommand(command);

                string response = processor.Response();

                _organiser.AddResponse(response);
            }
            catch (Exception ex)
            {
                _organiser.AddResponse($"Error: {ex.Message}");
            }

            _inputHandler.Clear();
            UpdateCommandLine();

            ScrollTerminalToBottom();
        }

        private void ScrollTerminalToBottom()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TerminalScrollViewer.ScrollToEnd();
            }));
        }

        private void Exit()
        {
            DoubleAnimation scale = new DoubleAnimation(
    1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut,
                }
            };
            PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
            Close();
        }
    }
}
