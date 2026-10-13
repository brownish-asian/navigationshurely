
using System;
using System.Windows;
using System.Windows.Input;

namespace navigationshurely
{
    public partial class HelpWindow : Window
    {
        private bool waitingForSecondG = false;

        // Adjust this to change how far j/k scroll.
        private const double ScrollAmount = 30;

        public HelpWindow()
        {
            InitializeComponent();
        }

        private void Window_PreviewKeyDown(
            object sender, KeyEventArgs e)
        {
            // Ctrl-based commands first.
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                switch (e.Key)
                {
                    case Key.U:
                        ScrollHalfPageUp();
                        e.Handled = true;
                        return;

                    case Key.D:
                        ScrollHalfPageDown();
                        e.Handled = true;
                        return;

                    case Key.F:
                        // Reserved for search.
                        e.Handled = true;
                        return;

                    default:
                        return;
                }
            }

            // Ignore modifier keys themselves.
            if (e.Key == Key.LeftShift ||
                e.Key == Key.RightShift ||
                e.Key == Key.LeftCtrl ||
                e.Key == Key.RightCtrl)
            {
                return;
            }

            // gg = top.
            if (waitingForSecondG)
            {
                waitingForSecondG = false;

                if (e.Key == Key.G)
                {
                    GoToTop();
                    e.Handled = true;
                    return;
                }
            }

            switch (e.Key)
            {
                case Key.J:
                    ScrollDown();
                    e.Handled = true;
                    break;

                case Key.K:
                    ScrollUp();
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    ScrollFullPageUp();
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    ScrollFullPageDown();
                    e.Handled = true;
                    break;

                case Key.G:
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        GoToBottom();
                    }
                    else
                    {
                        waitingForSecondG = true;
                    }

                    e.Handled = true;
                    break;

                case Key.Oem2:
                    // Reserved for / search.
                    e.Handled = true;
                    break;

                case Key.F:
                case Key.X:
                    Close();
                    e.Handled = true;
                    break;
            }
        }

        // Small scrolling: j / k.
        private void ScrollDown()
        {
            HelpScrollViewer.ScrollToVerticalOffset(
                HelpScrollViewer.VerticalOffset + ScrollAmount);
        }

        private void ScrollUp()
        {
            HelpScrollViewer.ScrollToVerticalOffset(
                HelpScrollViewer.VerticalOffset - ScrollAmount);
        }

        // Half-page scrolling: Ctrl+D / Ctrl+U.
        private void ScrollHalfPageDown()
        {
            HelpScrollViewer.ScrollToVerticalOffset(
                HelpScrollViewer.VerticalOffset
                + HelpScrollViewer.ViewportHeight / 2);
        }

        private void ScrollHalfPageUp()
        {
            HelpScrollViewer.ScrollToVerticalOffset(
                HelpScrollViewer.VerticalOffset
                - HelpScrollViewer.ViewportHeight / 2);
        }

        // Full-page scrolling.
        private void ScrollFullPageDown()
        {
            HelpScrollViewer.PageDown();
        }

        private void ScrollFullPageUp()
        {
            HelpScrollViewer.PageUp();
        }

        // Jump to the beginning or end.
        private void GoToTop()
        {
            HelpScrollViewer.ScrollToTop();
        }

        private void GoToBottom()
        {
            HelpScrollViewer.ScrollToBottom();
        }
    }
}
