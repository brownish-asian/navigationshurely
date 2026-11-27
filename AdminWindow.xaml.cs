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
using System.Windows.Threading;

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
        private VimEditorSession? _vimEditor;

        private VimMode? _lastVimMode;

        private bool _hiddenForFreeMove;
        private bool _freeMoveTransitionInProgress;

        public event Action? FreeMoveRequested;
        public event Action? FreeMoveTerminalHidden;

        private KingState kingState;
        private Librarian librarian;

        public AdminWindow(KingState kingState, Librarian librarian)
        {
            InitializeComponent();
            PreviewKeyDown += AdminWindow_PreviewKeyDown;
            TerminalOutput.ItemsSource = _organiser.Transcript;
            this.kingState = kingState;
            this.librarian = librarian;

            _inputHandler.WorkingDirectoryProvider =
    librarian.WorkingDirectory;

            processor = new Processor(librarian, _organiser.GetAllCommands);

            TerminalCompleter completer = new TerminalCompleter(librarian);

            _inputHandler.HistoryProvider = () => _organiser.History;
            _inputHandler.CompletionProvider = completer.GetCompletions;

            processor.ExitTerminal += Exit;
            processor.ClearTerminal += _organiser.ClearScreen;
            processor.VimRequested += OpenVimEditor;
            processor.FreeMoveRequested += QueueFreeMoveRequested;

            UpdateCommandLine();
            AdminInputDisplay.Focus();

            _inputHandler.PassTheMessageToSession += SendOffToZsh;
        }

        /// <summary>
        /// Gives the terminal input focus after it has been shown or restored.
        /// </summary>
        public void FocusCommandLine()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (!IsVisible || !IsLoaded)
                    return;

                // Activate the terminal even if a Vim session is open. Restore
                // focus to the current Vim surface rather than leaving focus on
                // the owner window after a FreeMove transition.
                Activate();

                if (_vimEditor != null)
                {
                    if (_vimEditor.Mode == VimMode.CommandLine)
                    {
                        VimCommandInput.Focus();
                        Keyboard.Focus(VimCommandInput);
                    }
                    else
                    {
                        VimBuffer.Focus();
                        Keyboard.Focus(VimBuffer);
                    }

                    return;
                }

                UpdateCommandLine();
                AdminInputDisplay.Focus();
                Keyboard.Focus(AdminInputDisplay);
            }));
        }

        private void QueueFreeMoveRequested()
        {
            // Defer the owner transition until command processing has finished
            // appending the response and clearing the terminal input line.
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    // A later command in the same command line may have closed
                    // the terminal before this queued transition is dispatched.
                    if (IsVisible && !_hiddenForFreeMove &&
                        !_freeMoveTransitionInProgress)
                    {
                        FreeMoveRequested?.Invoke();
                    }
                }));
        }

        /// <summary>
        /// Slides the terminal away, then hides (but does not close) this
        /// window so the owner can receive mouse input during free-move.
        /// </summary>
        public void AnimateOutForFreeMove()
        {
            if (_hiddenForFreeMove || _freeMoveTransitionInProgress || !IsVisible)
                return;

            _freeMoveTransitionInProgress = true;

            DoubleAnimation slideOut = new DoubleAnimation(
                0,
                ActualWidth + 40,
                TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseIn
                },
                FillBehavior = FillBehavior.Stop
            };

            slideOut.Completed += (_, _) =>
            {
                PopupSlide.BeginAnimation(TranslateTransform.XProperty, null);
                PopupSlide.X = 0;

                _hiddenForFreeMove = true;
                _freeMoveTransitionInProgress = false;

                Hide();
                FreeMoveTerminalHidden?.Invoke();
            };

            PopupSlide.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }

        /// <summary>
        /// Shows the same terminal instance again and restores command-line focus.
        /// </summary>
        public void RestoreFromFreeMove()
        {
            if (!_hiddenForFreeMove || _freeMoveTransitionInProgress)
                return;

            _hiddenForFreeMove = false;
            _freeMoveTransitionInProgress = true;

            double slideDistance = ActualWidth > 0 ? ActualWidth + 40 : Width + 40;

            PopupSlide.BeginAnimation(TranslateTransform.XProperty, null);
            PopupSlide.X = slideDistance;

            Show();

            DoubleAnimation slideIn = new DoubleAnimation(
                slideDistance,
                0,
                TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                },
                FillBehavior = FillBehavior.Stop
            };

            slideIn.Completed += (_, _) =>
            {
                PopupSlide.BeginAnimation(TranslateTransform.XProperty, null);
                PopupSlide.X = 0;
                _freeMoveTransitionInProgress = false;

                Activate();
                FocusCommandLine();
            };

            PopupSlide.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        private void UpdateCommandLine()
        {
            AdminInputDisplay.Text = _inputHandler.DisplayString();

            AdminInputDisplay.CaretIndex = _inputHandler.CursorIndex();

            if (_vimEditor == null)
                AdminInputDisplay.Focus();
        }

        private void OpenVimEditor(string path, string content)
        {
            if (_vimEditor != null)
                return;

            _vimEditor = new VimEditorSession(
                path,
                content,
                editedContent => librarian.WriteFile(path, editedContent));

            VimLayout.Visibility = Visibility.Visible;

            _lastVimMode = null;

            UpdateVimDisplay();
        }

        private void UpdateVimDisplay()
        {
            if (_vimEditor == null)
                return;

            // Convert the editor's internal line endings for WPF.
            string displayText = _vimEditor.Text.Replace("\n", "\r\n");

            if (!string.Equals(
                VimBuffer.Text,
                displayText,
                StringComparison.Ordinal))
            {
                VimBuffer.Text = displayText;
            }

            // Convert the editor's cursor index into the TextBox index.
            int displayCursor = _vimEditor.CursorPosition;

            for (int i = 0; i < _vimEditor.CursorPosition; i++)
            {
                if (_vimEditor.Text[i] == '\n')
                    displayCursor++;
            }

            VimBuffer.CaretIndex = Math.Clamp(
                displayCursor,
                0,
                VimBuffer.Text.Length);

            // Keep the cursor visible.
            int line = VimBuffer.GetLineIndexFromCharacterIndex(
                VimBuffer.CaretIndex);

            VimBuffer.ScrollToLine(line);

            // Update filename.
            string modifiedMarker = _vimEditor.IsModified
                ? " [+]"
                : string.Empty;

            VimFileTitle.Text =
                _vimEditor.FilePath + modifiedMarker;

            // Update mode.
            VimModeText.Text = _vimEditor.Mode switch
            {
                VimMode.Normal => "-- NORMAL --",
                VimMode.Insert => "-- INSERT --",
                VimMode.CommandLine => "-- COMMAND --",
                _ => string.Empty
            };

            // Update status.
            string position =
                $"Ln {_vimEditor.LineNumber}, Col {_vimEditor.ColumnNumber}";

            VimStatusText.Text = string.IsNullOrWhiteSpace(
                _vimEditor.StatusMessage)
                ? position
                : $"{_vimEditor.StatusMessage}    {position}";

            // Toggle command-line interface.
            bool commandMode =
                _vimEditor.Mode == VimMode.CommandLine;

            VimCommandRow.Visibility = commandMode
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Only change focus when changing modes.
            if (_lastVimMode != _vimEditor.Mode)
            {
                if (commandMode)
                {
                    VimCommandInput.Clear();
                    VimCommandInput.Focus();
                }
                else
                {
                    VimBuffer.Focus();
                }

                _lastVimMode = _vimEditor.Mode;
            }
        }

        private void VimBuffer_PreviewKeyDown(
    object sender,
    KeyEventArgs e)
        {
            if (_vimEditor == null)
                return;

            Key key = e.Key == Key.System
                ? e.SystemKey
                : e.Key;

            bool control = Keyboard.Modifiers.HasFlag(
                ModifierKeys.Control);

            // Ctrl+R: redo.
            if (control)
            {
                if (key == Key.R &&
                    _vimEditor.Mode == VimMode.Normal)
                {
                    _vimEditor.Redo();
                }

                e.Handled = true;

                UpdateVimDisplay();

                return;
            }

            // Escape.
            if (key == Key.Escape)
            {
                _vimEditor.Escape();

                e.Handled = true;

                UpdateVimDisplay();

                return;
            }

            // ---------------------------------------------------------
            // INSERT MODE
            // ---------------------------------------------------------

            if (_vimEditor.Mode == VimMode.Insert)
            {
                switch (key)
                {
                    case Key.Enter:
                        _vimEditor.InsertNewLine();
                        break;

                    case Key.Back:
                        _vimEditor.InsertBackspace();
                        break;

                    case Key.Delete:
                        _vimEditor.InsertDelete();
                        break;

                    case Key.Tab:
                        _vimEditor.InsertTab();
                        break;

                    case Key.Left:
                        _vimEditor.MoveInsertHorizontal(-1);
                        break;

                    case Key.Right:
                        _vimEditor.MoveInsertHorizontal(1);
                        break;

                    case Key.Up:
                        _vimEditor.MoveInsertVertical(-1);
                        break;

                    case Key.Down:
                        _vimEditor.MoveInsertVertical(1);
                        break;

                    case Key.Home:
                        _vimEditor.MoveInsertToLineStart();
                        break;

                    case Key.End:
                        _vimEditor.MoveInsertToLineEnd();
                        break;

                    default:
                        // Ordinary character input is handled by
                        // PreviewTextInput.
                        return;
                }

                e.Handled = true;

                UpdateVimDisplay();

                return;
            }

            // ---------------------------------------------------------
            // NORMAL MODE
            // ---------------------------------------------------------

            switch (key)
            {
                case Key.Left:
                    _vimEditor.HandleNormalCharacter('h');
                    break;

                case Key.Down:
                    _vimEditor.HandleNormalCharacter('j');
                    break;

                case Key.Up:
                    _vimEditor.HandleNormalCharacter('k');
                    break;

                case Key.Right:
                    _vimEditor.HandleNormalCharacter('l');
                    break;

                case Key.Home:
                    _vimEditor.HandleNormalCharacter('0');
                    break;

                case Key.End:
                    _vimEditor.HandleNormalCharacter('$');
                    break;

                case Key.Delete:
                    _vimEditor.HandleNormalCharacter('x');
                    break;

                case Key.Enter:
                case Key.Tab:
                case Key.Back:
                    break;

                default:
                    return;
            }

            e.Handled = true;

            UpdateVimDisplay();
        }

        private void VimBuffer_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_vimEditor == null)
                return;

            if (_vimEditor.Mode == VimMode.Insert)
            {
                _vimEditor.InsertText(e.Text);
            }
            else if (_vimEditor.Mode == VimMode.Normal)
            {
                foreach (char character in e.Text)
                {
                    _vimEditor.HandleNormalCharacter(character);
                }
            }

            e.Handled = true;

            UpdateVimDisplay();
        }

        private void VimCommandInput_PreviewKeyDown(
    object sender,
    KeyEventArgs e)
        {
            if (_vimEditor == null)
                return;

            if (e.Key == Key.Escape)
            {
                _vimEditor.CancelCommandLine();

                e.Handled = true;

                UpdateVimDisplay();

                return;
            }

            if (e.Key == Key.Enter)
            {
                bool shouldClose = _vimEditor.ExecuteColonCommand(
                    VimCommandInput.Text);

                e.Handled = true;

                if (shouldClose)
                {
                    CloseVimEditor();
                }
                else
                {
                    UpdateVimDisplay();
                }
            }
        }

        private void CloseVimEditor()
        {
            _vimEditor = null;

            _lastVimMode = null;

            VimLayout.Visibility = Visibility.Collapsed;

            UpdateCommandLine();

            AdminInputDisplay.Focus();
        }

        private void AdminWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Match MainWindow's global Ctrl+Q exit shortcut even while the
            // elevated terminal or its Vim editor owns keyboard focus.
            //if (e.Key == Key.Q &&
              //  (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            //{
              //  e.Handled = true;
                //Application.Current?.Shutdown();
            //}
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


            DoubleAnimation scale = new DoubleAnimation
            {
                From = 400,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut,
                }
            };
            PopupSlide.BeginAnimation(TranslateTransform.XProperty, scale);

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

        private bool _exitQueued;

        private void Exit()
        {
            if (_exitQueued)
                return;

            _exitQueued = true;

            // The processor is called from the input event. Closing the owned
            // window inside that event can interrupt WPF's focus/activation
            // hand-off while the command handler is still unwinding. Queue the
            // close so SendOffToZsh can finish updating the terminal first.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (IsLoaded)
                {
                    DoubleAnimation scale = new DoubleAnimation
                    {
                        From = 0,
                        To = 900,
                        Duration = TimeSpan.FromMilliseconds(500),
                        FillBehavior = FillBehavior.HoldEnd,
                        EasingFunction = new CubicEase
                        {
                            EasingMode = EasingMode.EaseOut,
                        }
                    };
                    scale.Completed += (s, e) =>
                    {
                        this.Visibility = Visibility.Hidden;
                        Close();
                    };
                    PopupSlide.BeginAnimation(TranslateTransform.XProperty, scale);
                }
                    
            }));
        }
    }
}
