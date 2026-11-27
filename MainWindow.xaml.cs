using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BibleApp.Data;
using navigationshurely.models;
using navigationshurely.Services;
using navigationshurely.services;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using navigationshurely.data;
using navigationshurely.terminal;

namespace navigationshurely
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    ///
    /// This class coordinates the WPF controls with InputHandler and
    /// BibleNavigator. Command parsing and Bible navigation remain in their
    /// existing classes; this window is responsible for presenting their state.
    /// </summary>
    public partial class MainWindow : Window
    {
        // =========================================================
        // CONSTANTS AND FIXED RESOURCES
        // =========================================================

        // Automatic sizing keeps the verse inside its normal display area.
        // The user's adjustment is applied after that fit calculation, so it
        // survives verse changes and window resizes.
        private const double MaxVerseFontSize = 90;
        private const double MinVerseFontSize = 30;
        private const double MaxManualVerseFontSize = 150;
        private const double VerseFontSizeStep = 2;

        private static readonly string[] Backgrounds =
        {
            "images/dbw59.jpg",
            "images/dbw02.jpg",
            "images/dbw04.jpg",
            "images/dbw08.jpg",
            "images/dbw16.jpg",
            "images/dbw17.jpg"
        };

        private readonly Brush normalBackgroundColor;
        private readonly Brush normalBorderBrush;
        private readonly Brush transparentBrush;
        private readonly Brush activeBorderBrush;

        // Reuse brushes for command colours instead of allocating one on
        // every key press. WPF brushes are frozen before being cached.
        private readonly Dictionary<Color, Brush> commandBrushes = new();

        // =========================================================
        // APP STATE
        // =========================================================

        private readonly InputHandler inputHandler;
        private readonly BibleDatabase _database;
        private readonly BibleNavigator navigator;
        private readonly AliasCatalogue _aliasCatalog = new();

        private readonly Librarian librarian =
            new Librarian("/home");

        private readonly KingState kingState;
        private readonly ShiftManager shiftManager;
        private readonly LayoutEditor layoutEditor;

        // The admin terminal remains alive while its visual is hidden for free-move.
        private AdminWindow? activeAdminWindow;

        // =========================================================
        // CHAPTER / VERSE COMMAND OPTIONS
        // =========================================================

        private readonly List<MovementEnderOptionItem> movementEnderOptions = new()
        {
            new MovementEnderOptionItem
            {
                Character = ' ',
                Shortcut = "SPACE",
                Meaning = " - absolute"
            },

            new MovementEnderOptionItem
            {
                Character = 'j',
                Shortcut = "j",
                Meaning = " - down"
            },

            new MovementEnderOptionItem
            {
                Character = 'k',
                Shortcut = "k",
                Meaning = " - up"
            }
        };

        private AppSettings plumber = null!;
        private string backgroundImagePath = "images/dbw59.jpg";
        private Layout layout = Layout.Classic;

        private bool globalUiVisible = true;
        private bool searchUiRequestedVisible;

        // Navigation hints are enabled by default to preserve the existing startup behaviour.
        // Search results, search filters and marked verses do not depend on this setting.
        private bool optionsVisible;
        private bool marksVisible;
        private bool enterVisible;
        private bool settingsSaved;

        // Avoid refreshing the main verse controls when a command has not
        // changed the current verse or translation.
        private int lastDisplayedBook = -1;
        private int lastDisplayedChapter = -1;
        private int lastDisplayedVerse = -1;
        private int lastDisplayedTranslation = -1;

        // The Guided layout needs a short, unique book code. Build those codes
        // once per translation instead of rebuilding all 66 names per key.
        private readonly Dictionary<int, string[]> guidedBookCodes = new();

        private int lastGuidedBook = -1;
        private int lastGuidedChapter = -1;
        private int lastGuidedVerse = -1;
        private int lastGuidedTranslation = -1;

        private double lastMeasuredVerseWidth = -1;
        private double lastMeasuredVerseHeight = -1;
        private double verseFontSizeAdjustment;

        // Track animation generations so interrupted animations cannot
        // accidentally hide controls that have since become visible.
        private readonly Dictionary<UIElement, int> visibilityAnimationVersions = new();

        private readonly Dictionary<UIElement, Visibility> visibilityAnimationTargets = new();

        // The guided command label uses a short, isolated transform animation.
        private int guidedLabelAnimationVersion;
        private bool guidedLabelAnimationRunning;

        // =========================================================
        // CONSTRUCTION AND LIFECYCLE
        // =========================================================

        public MainWindow()
        {
            InitializeComponent();

            SettingsGuy settingsGuy = new SettingsGuy();
            plumber = settingsGuy.loAd();
            try
            {
                librarian.RestoreFileSystem(plumber.TerminalFileSystem);

                // Keep the explicit setting as a compatibility fallback for
                // settings files written before the filesystem snapshot existed.
                if (!string.IsNullOrWhiteSpace(plumber.ActiveProfileName))
                    librarian.SelectProfile(plumber.ActiveProfileName);
            }
            catch (Exception ex)
            {
                // A malformed terminal snapshot must not prevent the Bible
                // application from starting. The librarian keeps its defaults
                // because restoration builds the replacement tree off to the side.
                MessageBox.Show(
                    $"The saved terminal filesystem could not be restored.\n\n{ex.Message}",
                    "Terminal settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            kingState = new KingState(librarian);
            DataContext = kingState;

            shiftManager = new ShiftManager(librarian);
            layoutEditor = new LayoutEditor(
                MainCanvas,
                layoutEditorOverlay,
                layoutSelectionOutline,
                layoutEditorStatus,
                guidedHudEditSurface,
                new KeyValuePair<LayoutResizeHandle, Rectangle>[]
                {
                    new(LayoutResizeHandle.NorthWest, layoutHandleNorthWest),
                    new(LayoutResizeHandle.North, layoutHandleNorth),
                    new(LayoutResizeHandle.NorthEast, layoutHandleNorthEast),
                    new(LayoutResizeHandle.East, layoutHandleEast),
                    new(LayoutResizeHandle.SouthEast, layoutHandleSouthEast),
                    new(LayoutResizeHandle.South, layoutHandleSouth),
                    new(LayoutResizeHandle.SouthWest, layoutHandleSouthWest),
                    new(LayoutResizeHandle.West, layoutHandleWest)
                },
                shiftManager);

            layoutEditor.RegisterRegion(Controls.TitleBox, titleBox);
            layoutEditor.RegisterRegion(Controls.TranslationBox, translationBox);
            layoutEditor.RegisterRegion(Controls.FrequencyBox, frequencyBox);
            layoutEditor.RegisterRegion(Controls.AliasBox, aliasBox);
            layoutEditor.RegisterRegion(Controls.ModeBox, modeBox);
            layoutEditor.RegisterRegion(Controls.VerseBox, verseContainer);
            layoutEditor.RegisterRegion(Controls.GuidedHud, guidedHudRegion);
            layoutEditor.RegisterRegion(Controls.RightList, rightListViewport);
            layoutEditor.RegisterRegion(Controls.BottomList, bottomListViewport);
            layoutEditor.RegisterEditSurface(Controls.TitleBox, titleBoxEditSurface);
            layoutEditor.RegisterEditSurface(Controls.TranslationBox, translationBoxEditSurface);
            layoutEditor.RegisterEditSurface(Controls.FrequencyBox, frequencyBoxEditSurface);
            layoutEditor.RegisterEditSurface(Controls.AliasBox, aliasBoxEditSurface);
            layoutEditor.RegisterEditSurface(Controls.ModeBox, modeBoxEditSurface);

            kingState.PropertyChanged += KingState_PropertyChanged;

            backgroundImagePath = plumber.LastImageFilePath;
            BackgroundBrush.ImageSource = new BitmapImage(
                new Uri(backgroundImagePath, UriKind.Relative));
            optionsVisible = plumber.LastOptionsHint;

            normalBackgroundColor = CreateFrozenBrush(
                Color.FromArgb(0x55, 0x07, 0x13, 0x20));
            normalBorderBrush = CreateFrozenBrush(
                Color.FromRgb(0x68, 0x78, 0x8C));
            transparentBrush = CreateFrozenBrush(
                Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
            activeBorderBrush = CreateFrozenBrush(
                Color.FromRgb(0xE5, 0xB9, 0x4F));

            inputHandler = new InputHandler(_aliasCatalog);
            _database = BibleDatabase.Master(plumber.LastTranslationNumber);
            navigator = new BibleNavigator(
                _database,
                plumber.LastMarks,
                plumber.LastBookNumber,
                plumber.LastChapterNumber,
                plumber.LastVerseNumber,
                _aliasCatalog);

            ConnectNavigatorEvents();
            ConnectInputEvents();

            layout = plumber.LastPreferredLayout;

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Activated += MainWindow_Activated;
            Loaded += MainWindow_Loaded;
        }

        public sealed class BookAliasSuggestionItem
        {
            public string Alias { get; set; } = string.Empty;
            public string BookTitle { get; set; } = string.Empty;
            public int BookId { get; set; }
        }

        public sealed class MovementQuantifierOptionItem
        {
            public char Character { get; set; }
            public string Shortcut { get; set; } = string.Empty;
            public string QuantityText { get; set; } = string.Empty;
        }

        public sealed class MovementEnderOptionItem
        {
            public char Character { get; set; }

            public string Shortcut { get; set; } = string.Empty;

            public string Meaning { get; set; } = string.Empty;
        }

        private void ConnectNavigatorEvents()
        {
            navigator.HelpRequested += OpenHelpWindow;
            navigator.CopyVerseText += CopyVerse;
            navigator.CopyVerseName += CopyTitle;
            navigator.CopyBothTitleAndText += CopyAll;
            navigator.DisplaySearchResults += DisplaySearch;
            navigator.FiltersHaveChanged += DisplayFilters;
            navigator.ChangeBackgroundPlease += ChangeBackgroundImage;
            navigator.InterfaceChange += SwitchLayouts;
            navigator.SearchUiVisibilityChanged += SetGlobalUiVisibility;
            navigator.UserScrolledSomewhere += ScrollRightList;
        }

        private void ConnectInputEvents()
        {
            // ExitApplication is an instance event in the supplied InputHandler.
            inputHandler.ExitApplication += CloseApplication;
            inputHandler.SearchUiVisibilityChanged += SetSearchUiRequestedVisibility;
            inputHandler.ShowMarks += ShowMarks;
            inputHandler.PressEnterVisibilityChanged += SetPressEnterVisibility;
            inputHandler.ToggleOptions += ToggleOptions;
            inputHandler.AdminSession += AdminSesh;
        }

        private void ToggleOptions()
        {
            // InputHandler raises this while ProcessKey is running. The normal
            // key path calls InterfaceUpdate immediately afterwards, so this
            // handler only changes state and avoids refreshing the lists twice.
            optionsVisible = !optionsVisible;
        }

        private void KingState_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(KingState.ActiveProfile))
            {
                layoutEditor.RefreshSelection();
                lastMeasuredVerseWidth = -1;
                lastMeasuredVerseHeight = -1;
                AdjustVerseFontSize(force: true);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            UpdateVerseDisplay(force: true);

            if (layout == Layout.Classic)
                ShowClassicLayout(Visibility.Visible);
            else
                ShowGuidedLayout(Visibility.Visible);

            InterfaceUpdate(commandWasProcessedByNavigator: false);
        }

        protected override void OnClosed(EventArgs e)
        {
            PrepareToClose();
            base.OnClosed(e);
        }

        private void CloseApplication()
        {
            Application.Current?.Shutdown();
        }

        private void PrepareToClose()
        {

            // Ctrl+Q calls this before Shutdown, and OnClosed calls it again.
            // Save once, using the navigator's current verse rather than a UI
            // field that might not have been populated yet.
            if (settingsSaved)
                return;

            Verse currentVerse = navigator.CurrentVerse;

            plumber.LastTranslationNumber = _database.TranslationNumber();
            plumber.LastMarks = navigator.BookMarks();
            plumber.LastBookNumber = currentVerse.BookId;
            plumber.LastChapterNumber = currentVerse.Chapter;
            plumber.LastVerseNumber = currentVerse.VerseNumber;
            plumber.LastImageFilePath = backgroundImagePath;
            plumber.LastPreferredLayout = layout;
            plumber.LastOptionsHint = optionsVisible;
            plumber.ActiveProfileName = librarian.CurrentProfile.Name;
            plumber.TerminalFileSystem = librarian.ExportFileSystem();

            SettingsGuy settingsGuy = new SettingsGuy();
            settingsGuy.saVe(plumber);
            settingsSaved = true;
        }

        // =========================================================
        // EVENT-DRIVEN INPUT
        // =========================================================

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Q is the application's global exit shortcut. Keep it
            // available while freemove owns keyboard input as well.
            if (e.Key == Key.Q &&
                (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                e.Handled = true;
                CloseApplication();
                return;
            }

            // The admin terminal is modeless so the owner can be used during
            // freemove. If the owner receives a key while the terminal is
            // visible in normal admin mode, return input to the admin session
            // instead of passing it to Bible navigation.
            AdminWindow? terminal = activeAdminWindow;
            if (!layoutEditor.IsEnabled &&
                terminal != null &&
                terminal.IsVisible)
            {
                e.Handled = true;
                terminal.FocusCommandLine();
                return;
            }

            // Free-move can only be entered through the admin terminal's
            // elevated `freemove` command. Escape is the sole editor exit path.
            if (layoutEditor.IsEnabled && e.Key == Key.Escape)
            {
                layoutEditor.SetEditMode(false);
                e.Handled = true;

                activeAdminWindow?.RestoreFromFreeMove();
                return;
            }

            // Layout editing owns keyboard input until it is exited. In
            // particular, do not pass editor keystrokes into Bible navigation
            // or the terminal command parser.
            if (layoutEditor.IsEnabled)
            {
                // Keep the standard Windows close shortcut available while
                // the editor temporarily owns application keyboard input.
                if (e.Key == Key.System && e.SystemKey == Key.F4)
                    return;

                e.Handled = true;
                return;
            }

            // Global verse zoom shortcuts are intercepted before the parser.
            // They therefore work in every mode, including partial aliases,
            // filters, query editing and absolute-path input.
            if (TryHandleVerseFontShortcut(e))
            {
                e.Handled = true;
                return;
            }

            EntireCommandStatus status = inputHandler.ProcessKey(e.Key);
            HandleInputResult(status);
            e.Handled = true;
        }

        /// <summary>
        /// Keeps the admin session in control whenever the owner is activated
        /// outside free-move, including an accidental Alt+Tab to MainWindow.
        /// </summary>
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            AdminWindow? terminal = activeAdminWindow;
            if (layoutEditor.IsEnabled || terminal == null)
                return;

            // Defer until the activation event has completed. The terminal may
            // be in the middle of being shown or closed at this point.
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    if (!layoutEditor.IsEnabled &&
                        ReferenceEquals(activeAdminWindow, terminal) &&
                        terminal.IsVisible)
                    {
                        terminal.FocusCommandLine();
                    }
                }));
        }

        private bool TryHandleVerseFontShortcut(KeyEventArgs e)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;

            bool controlHeld = (modifiers & ModifierKeys.Control) != 0;
            bool shiftHeld = (modifiers & ModifierKeys.Shift) != 0;

            if (!controlHeld)
                return false;

            // Ctrl+Shift+> / Ctrl+Shift+< on the main keyboard.
            if (shiftHeld && e.Key == Key.OemPeriod)
            {
                ChangeVerseFontSize(VerseFontSizeStep);
                return true;
            }

            if (shiftHeld && e.Key == Key.OemComma)
            {
                ChangeVerseFontSize(-VerseFontSizeStep);
                return true;
            }

            // Also support Ctrl+Numpad+/- and Ctrl+=/- as convenient aliases.
            switch (e.Key)
            {
                case Key.Add:
                case Key.OemPlus:
                    ChangeVerseFontSize(VerseFontSizeStep);
                    return true;

                case Key.Subtract:
                case Key.OemMinus:
                    ChangeVerseFontSize(-VerseFontSizeStep);
                    return true;

                default:
                    return false;
            }
        }

        private void ChangeVerseFontSize(double adjustment)
        {
            verseFontSizeAdjustment = Math.Clamp(
                verseFontSizeAdjustment + adjustment,
                MinVerseFontSize - MaxVerseFontSize,
                MaxManualVerseFontSize - MinVerseFontSize);

            // Force a fresh fit calculation so the manual adjustment is
            // immediately reflected, even when the window size is unchanged.
            AdjustVerseFontSize(force: true);
        }

        // Processes a character originating from a clickable UI option.
        // This deliberately uses the same parser as ordinary keyboard input.
        private void ProcessInputCharacter(char character)
        {
            EntireCommandStatus status = inputHandler.ProcessCharacter(character);

            HandleInputResult(status);
        }


        // Shared processing pipeline for keyboard and mouse input.
        private void HandleInputResult(EntireCommandStatus status)
        {
            if (status == EntireCommandStatus.Valid)
            {
                Command? command = inputHandler.GetCommand();

                if (command != null)
                {
                    navigator.ProcessCommand(command);

                    InterfaceUpdate(commandWasProcessedByNavigator: true);

                    UpdateVerseDisplay();

                    return;
                }
            }

            InterfaceUpdate(commandWasProcessedByNavigator: false);
        }

        private void AdminSesh()
        {
            if (navigator.IsAllowed())
            {
                ShowAdminTerminal();
            }
            else
            {
                navigator.ProcessCommand(inputHandler.GetCommand());
            }
        }

        private void GuidedModeBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (layout != Layout.Guided || sender is not FrameworkElement element)
                return;

            State? target = element.Tag?.ToString() switch
            {
                "B" => State.B,
                "C" => State.C,
                "V" => State.V,
                "S" => State.S,
                _ => null
            };

            if (target == null)
                return;

            // The semicolon label can be clicked while a command is being
            // entered; re-selecting S should not clear that in-progress input.
            // Keep the existing B/C/V click behavior unchanged.
            if (target.Value != State.S || inputHandler.getState() != State.S)
            {
                inputHandler.SelectNavigationMode(target.Value);

                // Refresh only the interface. No BibleNavigator command is needed.
                InterfaceUpdate(commandWasProcessedByNavigator: false);
            }

            e.Handled = true;
        }

        private void GuidedLabel_MouseEnter(object sender, MouseEventArgs e)
        {
            if (layout != Layout.Guided ||
                searchBorder.Visibility != Visibility.Visible ||
                guidedLabelAnimationRunning)
            {
                return;
            }

            // A small upward nudge replays the same easing on hover without
            // moving the label out of the screen or affecting parser work.
            AnimateGuidedLabelEntrance(fromScreen: false);
        }

        private void AnimateGuidedLabelEntrance(bool fromScreen)
        {
            if (searchBorder.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                searchBorder.RenderTransform = transform;
            }

            int version = ++guidedLabelAnimationVersion;
            guidedLabelAnimationRunning = true;

            double startOffset = fromScreen
                ? Math.Max(searchBorder.ActualHeight + 12.0, 44.0)
                : 14.0;

            transform.BeginAnimation(TranslateTransform.YProperty, null);
            searchBorder.BeginAnimation(UIElement.OpacityProperty, null);

            transform.Y = startOffset;
            searchBorder.Opacity = 0.0;

            TimeSpan middleTime = TimeSpan.FromMilliseconds(125);
            TimeSpan endTime = TimeSpan.FromMilliseconds(250);

            DoubleAnimationUsingKeyFrames slide = new();
            slide.KeyFrames.Add(new EasingDoubleKeyFrame(
                startOffset,
                KeyTime.FromTimeSpan(TimeSpan.Zero)));
            slide.KeyFrames.Add(new EasingDoubleKeyFrame(
                startOffset * 0.18,
                KeyTime.FromTimeSpan(middleTime))
            {
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame(
                0.0,
                KeyTime.FromTimeSpan(endTime))
            {
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });

            DoubleAnimationUsingKeyFrames fade = new();
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(
                0.0,
                KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0,
                KeyTime.FromTimeSpan(endTime))
            {
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });

            slide.Completed += (animationSender, animationArgs) =>
            {
                if (version != guidedLabelAnimationVersion)
                    return;

                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = 0.0;
                searchBorder.BeginAnimation(UIElement.OpacityProperty, null);
                searchBorder.Opacity = 1.0;
                guidedLabelAnimationRunning = false;
            };

            transform.BeginAnimation(
                TranslateTransform.YProperty,
                slide,
                HandoffBehavior.SnapshotAndReplace);
            searchBorder.BeginAnimation(
                UIElement.OpacityProperty,
                fade,
                HandoffBehavior.SnapshotAndReplace);
        }

        private void ResetGuidedLabelAnimation()
        {
            // Invalidates any completion callback still queued by an old run.
            guidedLabelAnimationVersion++;
            guidedLabelAnimationRunning = false;

            if (searchBorder.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = 0.0;
            }

            searchBorder.BeginAnimation(UIElement.OpacityProperty, null);
            searchBorder.Opacity = 1.0;
        }

        private void ShowAdminTerminal()
        {
            // Keep a single terminal instance so its history, transcript and
            // Vim session survive temporarily entering free-move mode.
            if (activeAdminWindow != null)
            {
                activeAdminWindow.Activate();
                activeAdminWindow.FocusCommandLine();
                return;
            }

            AdminWindow adminWindow = new AdminWindow(kingState, librarian)
            {
                Owner = this
            };

            activeAdminWindow = adminWindow;

            adminWindow.FreeMoveRequested += AdminWindow_FreeMoveRequested;
            adminWindow.FreeMoveTerminalHidden += AdminWindow_FreeMoveTerminalHidden;
            adminWindow.Closed += AdminWindow_Closed;

            // The terminal is modeless so the owner remains available during
            // free-move. No transparent overlay is placed over the main window.
            adminWindow.Show();
            adminWindow.FocusCommandLine();

            inputHandler.ExitSearchMode();
        }

        private void AdminWindow_FreeMoveRequested()
        {
            if (activeAdminWindow == null || layoutEditor.IsEnabled)
                return;

            // Prepare the editor before the terminal slides away.
            layoutEditor.SetEditMode(true);
            activeAdminWindow.AnimateOutForFreeMove();
        }

        private void AdminWindow_FreeMoveTerminalHidden()
        {
            if (!layoutEditor.IsEnabled)
                return;

            // Focus on the next dispatcher input pass, after the owned window's
            // Hide() and activation hand-off have finished.
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (!layoutEditor.IsEnabled || !IsVisible)
                        return;

                    Activate();
                    MainCanvas.Focus();
                    Keyboard.Focus(MainCanvas);
                }));
        }

        private void AdminWindow_Closed(object? sender, EventArgs e)
        {
            if (ReferenceEquals(sender, activeAdminWindow))
                activeAdminWindow = null;

            if (layoutEditor.IsEnabled)
                layoutEditor.SetEditMode(false);

            inputHandler.ExitSearchMode();

            // Explicitly hand keyboard focus back after WPF has completed the
            // owned-window close/activation transition.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (!IsVisible || activeAdminWindow != null)
                    return;

                Activate();
                MainCanvas.Focus();
                Keyboard.Focus(MainCanvas);
            }));
        }


        // =========================================================
        // LAYOUT VISIBILITY
        // =========================================================

        private void SwitchLayouts()
        {
            if (layout == Layout.Classic)
            {
                layout = Layout.Guided;
                ShowGuidedLayout(Visibility.Visible);
                UpdateGuidedVersePosition();
            }
            else
            {
                layout = Layout.Classic;
                ShowClassicLayout(Visibility.Visible);
            }
        }

        private void ShowGuidedLayout(Visibility visibility)
        {
            modeBox.Visibility = Visibility.Collapsed;
            bufferBox.Visibility = Visibility.Collapsed;
            hintBoxOne.Visibility = Visibility.Collapsed;
            hintBoxTwo.Visibility = Visibility.Collapsed;

            bookModeIndicator.Visibility = visibility;
            chapterModeIndicator.Visibility = visibility;
            verseModeIndicator.Visibility = visibility;

            guidedBookBox.Visibility = visibility;
            guidedChapterBox.Visibility = visibility;
            guidedVerseBox.Visibility = visibility;
            semiColonBox.Visibility = visibility;
            searchBorder.Visibility = visibility;
        }

        private void ShowClassicLayout(Visibility visibility)
        {
            modeBox.Visibility = visibility;
            bufferBox.Visibility = visibility;
            hintBoxOne.Visibility = visibility;
            hintBoxTwo.Visibility = visibility;

            bookModeIndicator.Visibility = Visibility.Collapsed;
            chapterModeIndicator.Visibility = Visibility.Collapsed;
            verseModeIndicator.Visibility = Visibility.Collapsed;

            guidedBookBox.Visibility = Visibility.Collapsed;
            guidedChapterBox.Visibility = Visibility.Collapsed;
            guidedVerseBox.Visibility = Visibility.Collapsed;
            semiColonBox.Visibility = Visibility.Collapsed;
            searchBorder.Visibility = Visibility.Collapsed;
        }

        private void SetGlobalUiVisibility(bool visible)
        {
            // BibleNavigator raises this event after toggling its own flag,
            // so the event argument is already the new visibility.
            SetGlobalUiVisibilityCore(visible);
        }

        private void SetGlobalUiVisibilityCore(bool visible)
        {
            globalUiVisible = visible;

            Visibility ordinaryControlsVisibility = visible
                ? Visibility.Visible
                : Visibility.Hidden;

            aliasBox.Visibility = ordinaryControlsVisibility;

            if (layout == Layout.Classic)
                ShowClassicLayout(ordinaryControlsVisibility);
            else
                ShowGuidedLayout(ordinaryControlsVisibility);

            ApplySearchUiVisibility();
        }

        // =========================================================
        // SEARCH AND MARK LIST VISIBILITY
        // =========================================================

        private void SetSearchUiRequestedVisibility(bool visible)
        {
            searchUiRequestedVisible = visible;

            if (visible)
            {
                // A previous chapter/verse suggestion list (or an old search)
                // must never masquerade as this session's search results.
                inputHandler.SetSearchResultsAvailable(false);
                rightList.SelectedItem = null;
                rightList.ItemsSource = null;
                rightList.ItemTemplate =
                    (DataTemplate)FindResource("SearchVerseTemplate");
                rightListBorder.BorderBrush = Brushes.Transparent;
                bottomListBorder.BorderBrush = Brushes.Transparent;
                frequencyBox.Text = string.Empty;
            }
            else
            {
                // Restore whichever right-hand list belonged to the mode
                // that was active before search was opened.
                if (marksVisible)
                    ApplyMarksVisibility();
                else
                    UpdateAliasSuggestions();
            }

            ApplySearchUiVisibility();
        }

        private void ShowMarks(bool visible)
        {
            marksVisible = visible;
            ApplyMarksVisibility();
        }

        private void SetPressEnterVisibility(bool visible)
        {
            // This is the single source of truth for the Enter prompt.
            // Always update the backing state before refreshing the UI.
            enterVisible = visible;
            ApplySearchUiVisibility();
        }

        private void ApplySearchUiVisibility()
        {
            Visibility visibility = globalUiVisible && searchUiRequestedVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyBottomListVisibility();

            SetAnimatedVisibility(frequencyBox, visibility);

            SetAnimatedVisibility(
                refreshSearchBox,
                globalUiVisible && searchUiRequestedVisible && enterVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed);

            if (searchUiRequestedVisible)
            {
                hintBoxTwo.Text = string.Empty;
                hintBoxOne.Text = "Press '/' again to exit Search Mode";
            }
            else
            {
                hintBoxOne.Text = "[space];f for help";
                hintBoxTwo.Text = "[space];e to toggle UI";
            }

            ApplyRightListVisibility();
        }

        private void ApplyBottomListVisibility()
        {
            List<string> activeFilters = searchUiRequestedVisible
                ? navigator.GetActiveSearchFilters()
                : new List<string>();

            bool showSearchFilters =
                globalUiVisible &&
                searchUiRequestedVisible &&
                activeFilters.Count > 0;

            bool showMovementEnders =
                globalUiVisible &&
                optionsVisible &&
                !searchUiRequestedVisible &&
                IsChapterOrVerseMode();

            if (showMovementEnders)
            {
                bottomList.ItemTemplate =
                    (DataTemplate)FindResource("MovementEnderTemplate");
                bottomList.ItemsSource = movementEnderOptions;
            }
            else if (showSearchFilters)
            {
                bottomList.ItemTemplate =
                    (DataTemplate)FindResource("BottomListTextTemplate");
                bottomList.ItemsSource = activeFilters;
            }
            else
            {
                // Do not leave movement-enders or filters in an inactive view.
                bottomList.SelectedItem = null;
                bottomList.ItemsSource = null;
            }

            Visibility visibility =
                showSearchFilters || showMovementEnders
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Animate the container too: otherwise its BorderBrush can remain
            // visible as a small empty outline after the ListView collapses.
            SetAnimatedVisibility(bottomListBorder, visibility);
            SetAnimatedVisibility(bottomList, visibility);
        }

        private void ApplyMarksVisibility()
        {
            if (marksVisible)
            {
                rightList.ItemTemplate =
                    (DataTemplate)FindResource("MarkedVerseTemplate");

                rightList.ItemsSource = navigator.BookMarks()
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new MarkedVerseItem
                    {
                        KeyCode = pair.Key,
                        Verse = pair.Value
                    })
                    .ToList();
            }

            // Clearing is handled by ApplyRightListVisibility after the fade-out.
            ApplyRightListVisibility();
        }

        private void ClearRightListIfUnused()
        {
            // Search results and marked verses must never be cleared here.
            if (marksVisible || searchUiRequestedVisible)
                return;

            // Navigation suggestions are only in use while their option is enabled.
            if (optionsVisible &&
                (IsChapterOrVerseMode() ||
                 inputHandler.BookAliasSuggestionPrefix() != null ||
                 inputHandler.AliasSuggestionPrefix() != null))
            {
                return;
            }

            rightList.SelectedItem = null;
            rightList.ItemsSource = null;
        }

        private void UpdateAliasSuggestions()
        {
            // Don't interfere with other uses of the right-hand list.
            // Also avoid generating suggestions when the entire UI is hidden.
            if (!globalUiVisible || !optionsVisible || marksVisible || searchUiRequestedVisible)
                return;


            // =========================================================
            // BOOK MODE SUGGESTIONS
            // =========================================================

            string? bookPrefix = inputHandler.BookAliasSuggestionPrefix();

            if (bookPrefix != null)
            {
                rightList.ItemTemplate =
                    (DataTemplate)FindResource("BookAliasSuggestionTemplate");

                rightList.ItemsSource =
                    inputHandler.GetBookAliasSuggestions(bookPrefix)
                        .Select(pair => new BookAliasSuggestionItem
                        {
                            Alias = pair.Key,

                            BookId = pair.Value,

                            BookTitle = new Verse
                            {
                                BookId = pair.Value
                            }.Title()
                        })
                        .ToList();

                return;
            }

            // =========================================================
            // CHAPTER / VERSE MOVEMENT OPTIONS
            // =========================================================

            if (IsChapterOrVerseMode())
            {
                rightList.ItemTemplate =
                    (DataTemplate)FindResource("MovementQuantifierTemplate");

                rightList.ItemsSource =
                    inputHandler.GetMovementQuantifierOptions()
                        .Select(pair => new MovementQuantifierOptionItem
                        {
                            Character = pair.Key,

                            Shortcut = pair.Key.ToString(),

                            QuantityText = pair.Value >= 10
                                ? $" - {pair.Value} (fast)"
                                : $" - {pair.Value}"
                        })
                        .ToList();

                return;
            }


            // =========================================================
            // SEMICOLON MODE SUGGESTIONS
            // =========================================================

            string? prefix = inputHandler.AliasSuggestionPrefix();

            if (prefix == null)
                return;

            rightList.ItemTemplate =
                (DataTemplate)FindResource("AliasSuggestionTemplate");

            rightList.ItemsSource =
                navigator.GetAliasSuggestions(prefix);
        }

        private void ApplyRightListVisibility()
        {
            bool showSearchResults =
                searchUiRequestedVisible &&
                inputHandler.getState() == State.Choice &&
                inputHandler.SearchResultsAvailable &&
                rightList.Items.Count > 0;

            bool showNavigationOptions =
                optionsVisible &&
                (
                    IsChapterOrVerseMode() ||
                    inputHandler.BookAliasSuggestionPrefix() != null ||
                    inputHandler.AliasSuggestionPrefix() != null
                );

            bool shouldBeVisible =
                globalUiVisible &&
                (
                    showSearchResults ||
                    (!searchUiRequestedVisible && marksVisible) ||
                    showNavigationOptions
                );

            if (shouldBeVisible)
            {
                SetAnimatedVisibility(
                    rightListBorder,
                    Visibility.Visible);

                return;
            }

            bool alreadyHidden =
                rightListBorder.Visibility != Visibility.Visible;

            SetAnimatedVisibility(
                rightListBorder,
                Visibility.Collapsed,
                ClearRightListIfUnused);

            // If the list was already hidden, there won't be another
            // animation completion to invoke the clearing callback.
            if (alreadyHidden)
                ClearRightListIfUnused();
        }

        private bool IsChapterOrVerseMode()
        {
            State currentState = inputHandler.getState();

            return currentState == State.C ||
                   currentState == State.V;
        }

        // =========================================================
        // COMMAND-STATE PRESENTATION
        // =========================================================

        private void InterfaceUpdate(bool commandWasProcessedByNavigator)
        {
            if (layout == Layout.Classic)
                UpdateClassicInterface(commandWasProcessedByNavigator);
            else
                UpdateGuidedInterface(commandWasProcessedByNavigator);

            UpdateAliasSuggestions();

            ApplyRightListVisibility();

            ApplyBottomListVisibility();
        }

        private void UpdateClassicInterface(bool commandWasProcessedByNavigator)
        {
            modeBox.Text = inputHandler.GetStateText();
            bufferBox.Text = inputHandler.CommandInfo();

            State currentState = inputHandler.getState();
            UpdateClassicBorders(currentState);

            bufferBox.Background = GetCommandBrush(
                commandWasProcessedByNavigator,
                classic: true);
        }

        private void UpdateClassicBorders(State currentState)
        {
            rightListBorder.BorderBrush = Brushes.Transparent;
            bottomListBorder.BorderBrush = Brushes.Transparent;
            bufferBoxBorder.BorderBrush = transparentBrush;

            switch (currentState)
            {
                case State.Filter:
                    if (bottomList.Items.Count > 0)
                    {
                        bottomListBorder.BorderBrush = activeBorderBrush;
                    }
                    break;

                case State.Choice:
                    if (inputHandler.SearchResultsAvailable &&
                        rightList.Items.Count > 0)
                    {
                        rightListBorder.BorderBrush = activeBorderBrush;
                    }
                    break;

                case State.Query:
                    bufferBoxBorder.BorderBrush = activeBorderBrush;
                    break;
            }
        }

        private void UpdateGuidedInterface(bool commandWasProcessedByNavigator)
        {
            UpdateGuidedVersePosition();

            State currentState = inputHandler.getState();
            string commandInfo = inputHandler.CommandInfo();

            switch (currentState)
            {
                case State.B:
                    bsBox.Text = commandInfo;
                    csBox.Text = string.Empty;
                    vsBox.Text = string.Empty;
                    guidedLabel.Text = string.Empty;

                    SetGuidedModeAppearance(
                        State.B,
                        GetCommandBrush(commandWasProcessedByNavigator, classic: false));
                    ResetGuidedLabelAnimation();
                    SetAnimatedVisibility(searchBorder, Visibility.Collapsed);
                    break;

                case State.C:
                    bsBox.Text = string.Empty;
                    csBox.Text = commandInfo;
                    vsBox.Text = string.Empty;
                    guidedLabel.Text = string.Empty;

                    SetGuidedModeAppearance(
                        State.C,
                        GetCommandBrush(commandWasProcessedByNavigator, classic: false));
                    ResetGuidedLabelAnimation();
                    SetAnimatedVisibility(searchBorder, Visibility.Collapsed);
                    break;

                case State.V:
                    bsBox.Text = string.Empty;
                    csBox.Text = string.Empty;
                    vsBox.Text = commandInfo;
                    guidedLabel.Text = string.Empty;

                    SetGuidedModeAppearance(
                        State.V,
                        GetCommandBrush(commandWasProcessedByNavigator, classic: false));
                    ResetGuidedLabelAnimation();
                    SetAnimatedVisibility(searchBorder, Visibility.Collapsed);
                    break;

                case State.S:
                case State.Choice:
                case State.Query:
                case State.Filter:
                    UpdateGuidedSearchInterface(
                        currentState,
                        commandInfo,
                        commandWasProcessedByNavigator);
                    break;
            }
        }

        private void SetGuidedModeAppearance(State activeState, Brush activeCommandBrush)
        {
            // Reset search-related highlights.
            rightListBorder.BorderBrush = Brushes.Transparent;
            bottomListBorder.BorderBrush = Brushes.Transparent;
            searchBorder.BorderBrush = Brushes.Transparent;

            // Update mode indicator backgrounds.
            bookModeIndicator.Background = activeState == State.B
                ? activeCommandBrush
                : normalBackgroundColor;

            chapterModeIndicator.Background = activeState == State.C
                ? activeCommandBrush
                : normalBackgroundColor;

            verseModeIndicator.Background = activeState == State.V
                ? activeCommandBrush
                : normalBackgroundColor;

            // Update mode indicator borders.
            bBorder.BorderBrush = activeState == State.B
                ? activeBorderBrush
                : normalBorderBrush;

            cBorder.BorderBrush = activeState == State.C
                ? activeBorderBrush
                : normalBorderBrush;

            vBorder.BorderBrush = activeState == State.V
                ? activeBorderBrush
                : normalBorderBrush;
        }

        private void UpdateGuidedSearchInterface(State currentState, string commandInfo, bool commandWasProcessedByNavigator)
        {
            Visibility visibility = globalUiVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

            bool wasVisible = searchBorder.Visibility == Visibility.Visible;
            guidedLabel.Visibility = Visibility.Visible;
            SetAnimatedVisibility(searchBorder, visibility);

            if (visibility == Visibility.Visible && !wasVisible)
            {
                // The label is anchored at the bottom of the HUD. A short
                // translate/fade brings it up into view only on first entry.
                AnimateGuidedLabelEntrance(fromScreen: true);
            }
            else if (visibility != Visibility.Visible)
            {
                ResetGuidedLabelAnimation();
            }

            bsBox.Text = string.Empty;
            csBox.Text = string.Empty;
            vsBox.Text = string.Empty;

            bookModeIndicator.Background = normalBackgroundColor;
            chapterModeIndicator.Background = normalBackgroundColor;
            verseModeIndicator.Background = normalBackgroundColor;

            bBorder.BorderBrush = normalBorderBrush;
            cBorder.BorderBrush = normalBorderBrush;
            vBorder.BorderBrush = normalBorderBrush;

            guidedLabel.Text = commandInfo;
            guidedLabel.Background = GetCommandBrush(
                commandWasProcessedByNavigator,
                classic: true);

            rightListBorder.BorderBrush = Brushes.Transparent;
            bottomListBorder.BorderBrush = Brushes.Transparent;
            searchBorder.BorderBrush = Brushes.Transparent;

            switch (currentState)
            {
                case State.Filter:
                    if (bottomList.Items.Count > 0)
                    {
                        bottomListBorder.BorderBrush = activeBorderBrush;
                    }
                    break;

                case State.Choice:
                    if (inputHandler.SearchResultsAvailable &&
                        rightList.Items.Count > 0)
                    {
                        rightListBorder.BorderBrush = activeBorderBrush;
                    }
                    break;

                default:
                    searchBorder.BorderBrush = activeBorderBrush;
                    break;
            }
        }

        private Brush GetCommandBrush(bool commandWasProcessedByNavigator, bool classic)
        {
            Color color = commandWasProcessedByNavigator
                ? navigator.CommandColor()
                : inputHandler.CommandColor(classic);

            return GetCachedBrush(color);
        }

        private Brush GetCachedBrush(Color color)
        {
            if (commandBrushes.TryGetValue(color, out Brush? brush))
                return brush;

            Brush newBrush = CreateFrozenBrush(color);
            commandBrushes[color] = newBrush;
            return newBrush;
        }

        // =========================================================
        // VERSE DISPLAY
        // =========================================================

        private void UpdateVerseDisplay(bool force = false)
        {
            Verse currentVerse = navigator.CurrentVerse;
            int translationNumber = _database.TranslationNumber();

            bool verseIsUnchanged =
                currentVerse.BookId == lastDisplayedBook &&
                currentVerse.Chapter == lastDisplayedChapter &&
                currentVerse.VerseNumber == lastDisplayedVerse &&
                translationNumber == lastDisplayedTranslation;

            if (!force && verseIsUnchanged)
            {
                // A resize can require a new font size even when the verse
                // itself has not changed. This method skips measuring unless
                // the available text area has actually changed.
                AdjustVerseFontSize();
                UpdateGuidedVersePosition();
                return;
            }

            titleBox.Text = currentVerse.ToString();
            verseBox.Text = currentVerse.Text;
            translationBox.Text = _database.TranslationName();
            aliasBox.Text = navigator.AliasForCurrentVerse();

            AdjustVerseFontSize(force: true);

            lastDisplayedBook = currentVerse.BookId;
            lastDisplayedChapter = currentVerse.Chapter;
            lastDisplayedVerse = currentVerse.VerseNumber;
            lastDisplayedTranslation = translationNumber;

            UpdateGuidedVersePosition();
        }

        private void UpdateGuidedVersePosition()
        {
            if (layout != Layout.Guided)
                return;

            Verse currentVerse = navigator.CurrentVerse;
            int translationNumber = _database.TranslationNumber();

            if (currentVerse.BookId == lastGuidedBook &&
                currentVerse.Chapter == lastGuidedChapter &&
                currentVerse.VerseNumber == lastGuidedVerse &&
                translationNumber == lastGuidedTranslation)
            {
                return;
            }

            string[] bookCodes = GetGuidedBookCodes(translationNumber);
            string bookCode = bookCodes[currentVerse.BookId - 1];

            guidedBookBox.Text = bookCode;
            guidedChapterBox.Text = currentVerse.Chapter.ToString();
            guidedVerseBox.Text = currentVerse.VerseNumber.ToString();

            lastGuidedBook = currentVerse.BookId;
            lastGuidedChapter = currentVerse.Chapter;
            lastGuidedVerse = currentVerse.VerseNumber;
            lastGuidedTranslation = translationNumber;
        }

        private string[] GetGuidedBookCodes(int translationNumber)
        {
            if (guidedBookCodes.TryGetValue(translationNumber, out string[]? cachedCodes))
                return cachedCodes;

            string[] titles = Enumerable.Range(1, 66)
                .Select(id => new Verse { BookId = id }.Title())
                .ToArray();

            string[] names = titles
                .Select(title => new string(
                    title.Where(char.IsLetterOrDigit).ToArray()))
                .ToArray();

            // If a complete book name begins with another complete book name,
            // use its initials to avoid ambiguous prefixes (for example AWIT).
            for (int i = 0; i < names.Length; i++)
            {
                bool startsWithAnotherTitle = false;

                for (int j = 0; j < names.Length; j++)
                {
                    if (i == j)
                        continue;

                    if (names[i].Length > names[j].Length &&
                        names[i].StartsWith(names[j], StringComparison.Ordinal))
                    {
                        startsWithAnotherTitle = true;
                        break;
                    }
                }

                if (startsWithAnotherTitle)
                {
                    names[i] = string.Concat(
                        titles[i]
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => word[0]));
                }
            }

            int codeLength = 1;

            for (; codeLength <= 5; codeLength++)
            {
                string[] candidateCodes = names
                    .Select(name => name[..Math.Min(codeLength, name.Length)])
                    .ToArray();

                if (candidateCodes.Distinct(StringComparer.Ordinal).Count() == 66)
                    break;
            }

            if (codeLength > 5)
            {
                throw new InvalidOperationException(
                    "Could not uniquely identify every book.");
            }

            string[] result = names
                .Select(name => name[..Math.Min(codeLength, name.Length)])
                .ToArray();

            guidedBookCodes[translationNumber] = result;
            return result;
        }

        private void verseContainer_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (e.WidthChanged || e.HeightChanged)
            {
                lastMeasuredVerseWidth = -1;
                lastMeasuredVerseHeight = -1;
                AdjustVerseFontSize(force: true);
            }
        }

        private void AdjustVerseFontSize(bool force = false)
        {
            if (verseBox == null || verseContainer == null)
                return;

            if (string.IsNullOrWhiteSpace(verseBox.Text))
                return;

            double availableWidth = verseContainer.ActualWidth;
            double availableHeight = verseContainer.ActualHeight;

            if (availableWidth <= 0 || availableHeight <= 0)
                return;

            if (!force &&
                availableWidth == lastMeasuredVerseWidth &&
                availableHeight == lastMeasuredVerseHeight)
            {
                return;
            }

            double low = MinVerseFontSize;
            double high = MaxVerseFontSize;

            while (high - low > 1)
            {
                double middle = (low + high) / 2;
                verseBox.FontSize = middle;

                verseBox.Measure(new Size(
                    availableWidth,
                    double.PositiveInfinity));

                if (verseBox.DesiredSize.Height <= availableHeight)
                    low = middle;
                else
                    high = middle;
            }

            verseBox.FontSize = Math.Clamp(
                low + verseFontSizeAdjustment,
                MinVerseFontSize,
                MaxManualVerseFontSize);
            lastMeasuredVerseWidth = availableWidth;
            lastMeasuredVerseHeight = availableHeight;
        }

        // =========================================================
        // SEARCH RESULTS AND FILTERS
        // =========================================================

        private void DisplaySearch(State state)
        {
            List<Verse> searchResults = navigator.LocatedVerses();

            rightList.SelectedItem = null;
            rightList.ItemTemplate =
                (DataTemplate)FindResource("SearchVerseTemplate");
            rightList.ItemsSource = searchResults;
            inputHandler.SetSearchResultsAvailable(searchResults.Count > 0);

            int frequency = searchResults.Count;
            string query = navigator.ActualSearchQuery();

            if (frequency == 1)
            {
                frequencyBox.Text = $"\"{query}\" : 1 result found";
            }
            else if (frequency > 1)
            {
                frequencyBox.Text = $"\"{query}\" : {frequency} results found";
            }
            else
            {
                frequencyBox.Text = $"\"{query}\" : No results found";
            }
        }

        private void DisplayFilters()
        {
            bottomList.ItemTemplate =
                (DataTemplate)FindResource("BottomListTextTemplate");

            bottomList.ItemsSource =
                navigator.GetActiveSearchFilters();
        }

        private void ScrollRightList()
        {
            int selectedSearchIndex = navigator.SelectedSearchIndex();

            if (selectedSearchIndex >= 0 &&
                selectedSearchIndex < rightList.Items.Count)
            {
                rightList.ScrollIntoView(rightList.Items[selectedSearchIndex]);
            }
        }

        private void rightList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? clickedElement = e.OriginalSource as DependencyObject;

            ListViewItem? clickedItem = FindParent<ListViewItem>(clickedElement);

            if (clickedItem == null)
                return;

            object item = rightList.ItemContainerGenerator
                .ItemFromContainer(clickedItem);

            // SEARCH RESULT CLICK
            if (item is Verse clickedVerse)
            {
                navigator.SetCurrentVerse(clickedVerse);
                UpdateVerseDisplay();
            }

            // BOOK ALIAS SUGGESTION CLICK
            else if (item is BookAliasSuggestionItem clickedBookAlias)
            {
                // Clear the unfinished book alias from the input parser.
                inputHandler.BookAliasSuggestionSelected();

                // Execute the same navigation command as a completed book alias.
                navigator.ProcessCommand(new Command
                {
                    Mode = State.B,
                    Quantity = clickedBookAlias.BookId,
                    Direction = Direct.Absolute
                });

                // Update the displayed verse.
                UpdateVerseDisplay();

                // Refresh the interface and remove the suggestions.
                InterfaceUpdate(commandWasProcessedByNavigator: false);
            }

            // CHAPTER / VERSE QUANTIFIER CLICK
            else if (item is MovementQuantifierOptionItem clickedQuantifier)
            {
                ProcessInputCharacter(clickedQuantifier.Character);

                e.Handled = true;
            }

            // ALIAS SUGGESTION CLICK
            else if (item is AliasSuggestion clickedAlias)
            {
                // Navigate using the selected alias.
                bool successful = navigator.SetCurrentVerseByAlias(clickedAlias.Alias);

                if (!successful)
                    return;

                // Clear the unfinished alias command.
                inputHandler.AliasSuggestionSelected();

                // Refresh the displayed verse.
                UpdateVerseDisplay();

                // Refresh the interface and remove the alias suggestions.
                InterfaceUpdate(commandWasProcessedByNavigator: false);
            }
        }

        private void titleBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ToggleOptions();

            // A mouse click bypasses the normal keyboard update cycle.
            // Keep search and marked-verse views completely untouched.
            if (!searchUiRequestedVisible && !marksVisible)
            {
                // Only generate suggestions when turning the hints back on.
                if (optionsVisible)
                    UpdateAliasSuggestions();

                ApplyRightListVisibility();
                ApplyBottomListVisibility();
            }

            e.Handled = true;
        }

        private void translationBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            navigator.ProcessCommand(new Command
            {
                Mode = State.S,
                Quantity = 300,
                Direction = Direct.Absolute
            });
            e.Handled = true;
        }

        private void verseBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ChangeVerseFontSize(-VerseFontSizeStep);
            e.Handled = true;
        }

        private void verseBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ChangeVerseFontSize(VerseFontSizeStep);
            e.Handled = true;
        }

        private void bottomList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? clickedElement =
                e.OriginalSource as DependencyObject;

            ListViewItem? clickedItem =
                FindParent<ListViewItem>(clickedElement);

            if (clickedItem == null)
                return;

            object item = bottomList.ItemContainerGenerator
                .ItemFromContainer(clickedItem);


            // CHAPTER / VERSE COMMAND ENDER CLICK
            if (item is MovementEnderOptionItem clickedEnder)
            {
                ProcessInputCharacter(clickedEnder.Character);

                e.Handled = true;
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        // =========================================================
        // BACKGROUND, HELP, AND CLIPBOARD
        // =========================================================

        private void ChangeBackgroundImage(Direct direction)
        {
            int index = Array.IndexOf(Backgrounds, backgroundImagePath);

            if (index < 0)
                index = 0;

            int step = direction switch
            {
                Direct.Up => 1,
                Direct.Down => -1,
                _ => 0
            };

            index = (index + step + Backgrounds.Length) % Backgrounds.Length;
            backgroundImagePath = Backgrounds[index];

            BackgroundBrush.ImageSource = new BitmapImage(
                new Uri(backgroundImagePath, UriKind.Relative));
        }

        private void OpenHelpWindow()
        {
            HelpLoadingSplash loadingSplash = new HelpLoadingSplash();
            bool splashClosed = false;

            // Close it exactly once. This method is called both when the
            // HelpWindow first renders and as a safety net in finally.
            void CloseLoadingSplash()
            {
                if (splashClosed)
                    return;

                splashClosed = true;
                loadingSplash.Close();
            }

            try
            {
                // The splash has its own STA thread, so its animation keeps
                // running while the main UI thread constructs the help UI.
                loadingSplash.Show();

                // Keep creating WPF controls on the main window's UI thread.
                HelpWindow helpWindow = new HelpWindow
                {
                    Owner = this
                };

                // InitializeComponent() only constructs the XAML tree. WPF
                // still has to measure, arrange and render the window after
                // ShowDialog(). ContentRendered marks that first real render.
                helpWindow.ContentRendered += (_, _) => CloseLoadingSplash();

                helpWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The help manual could not be opened.\n\n" + ex.Message,
                    "Help window error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Also close the splash if construction/rendering failed.
                CloseLoadingSplash();
            }
        }

        private void CopyVerse()
        {
            Clipboard.SetText(verseBox.Text);
        }

        private void CopyTitle()
        {
            Clipboard.SetText(titleBox.Text);
        }

        private void CopyAll()
        {
            Clipboard.SetText(titleBox.Text + "\n" + verseBox.Text);
        }

        private sealed class HelpLoadingSplash
        {
            private readonly ManualResetEventSlim _ready = new(false);

            private Dispatcher? _dispatcher;
            private Window? _window;
            private Exception? _startupException;

            public void Show()
            {
                Thread splashThread = new Thread(() =>
                {
                    try
                    {
                        // Every WPF UI thread needs its own Dispatcher.
                        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

                        _dispatcher = dispatcher;

                        Window splash = CreateWindow();

                        _window = splash;

                        // Automatically shut down this thread when the
                        // loading window closes.
                        splash.Closed += (_, _) =>
                        {
                            if (!dispatcher.HasShutdownStarted &&
                                !dispatcher.HasShutdownFinished)
                            {
                                dispatcher.BeginInvokeShutdown(
                                    DispatcherPriority.Background);
                            }
                        };

                        splash.Show();

                        // Signal readiness after the dispatcher has had
                        // an opportunity to process the initial rendering.
                        dispatcher.BeginInvoke(
                            DispatcherPriority.ApplicationIdle,
                            new Action(_ready.Set));

                        // Keep the splash window's animation running.
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        _startupException = ex;
                        _ready.Set();
                    }
                });

                // WPF requires an STA thread.
                splashThread.SetApartmentState(ApartmentState.STA);

                splashThread.IsBackground = true;

                splashThread.Start();

                // Wait only until the loading window is ready,
                // NOT until the HelpWindow has finished loading.
                _ready.Wait();

                if (_startupException != null)
                {
                    throw new InvalidOperationException(
                        "Could not start the help loading screen.",
                        _startupException);
                }
            }


            public void Close()
            {
                Dispatcher? dispatcher = _dispatcher;

                if (dispatcher == null ||
                    dispatcher.HasShutdownStarted ||
                    dispatcher.HasShutdownFinished)
                {
                    return;
                }

                // Close the window on its own UI thread.
                dispatcher.Invoke(() =>
                {
                    if (_window?.IsVisible == true)
                        _window.Close();
                });
            }

            private static Window CreateWindow()
            {
                // =====================================================
                // HEADING
                // =====================================================

                TextBlock heading = new TextBlock
                {
                    Text = "OPENING HELP MANUAL",

                    Foreground = Brushes.White,

                    FontFamily = new FontFamily("Consolas"),

                    FontSize = 21,

                    FontWeight = FontWeights.Bold,

                    HorizontalAlignment = HorizontalAlignment.Center
                };


                // =====================================================
                // MAIN MESSAGE
                // =====================================================

                TextBlock message = new TextBlock
                {
                    Text = "Preparing your navigation reference.",

                    Foreground = new SolidColorBrush(
                        Color.FromRgb(190, 200, 215)),

                    FontFamily = new FontFamily("Consolas"),

                    FontSize = 13,

                    Margin = new Thickness(0, 10, 0, 0),

                    HorizontalAlignment = HorizontalAlignment.Center
                };


                // =====================================================
                // ANIMATED PROGRESS BAR
                // =====================================================

                ProgressBar progress = new ProgressBar
                {
                    IsIndeterminate = true,

                    Height = 8,

                    Margin = new Thickness(0, 24, 0, 14),

                    Foreground = new SolidColorBrush(
                        Color.FromRgb(255, 213, 79)),

                    Background = new SolidColorBrush(
                        Color.FromRgb(55, 63, 75)),

                    BorderThickness = new Thickness(0)
                };


                // =====================================================
                // FOOTER
                // =====================================================

                TextBlock footer = new TextBlock
                {
                    Text = "...",

                    Foreground = new SolidColorBrush(
                        Color.FromRgb(125, 140, 160)),

                    FontFamily = new FontFamily("Consolas"),

                    FontSize = 11,

                    HorizontalAlignment = HorizontalAlignment.Center
                };


                // =====================================================
                // CONTENT LAYOUT
                // =====================================================

                StackPanel content = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,

                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                content.Children.Add(heading);
                content.Children.Add(message);
                content.Children.Add(progress);
                content.Children.Add(footer);


                Border panel = new Border
                {
                    Background = new SolidColorBrush(
                        Color.FromRgb(18, 24, 34)),

                    BorderBrush = new SolidColorBrush(
                        Color.FromRgb(75, 88, 108)),

                    BorderThickness = new Thickness(1),

                    CornerRadius = new CornerRadius(12),

                    Padding = new Thickness(32),

                    Child = content
                };


                // =====================================================
                // LOADING WINDOW
                // =====================================================

                return new Window
                {
                    Title = "Opening Help",

                    Width = 480,

                    Height = 210,

                    WindowStyle = WindowStyle.None,

                    ResizeMode = ResizeMode.NoResize,

                    WindowStartupLocation = WindowStartupLocation.CenterScreen,

                    ShowInTaskbar = false,

                    ShowActivated = true,

                    Topmost = true,

                    Background = new SolidColorBrush(
                        Color.FromRgb(18, 24, 34)),

                    Content = panel
                };
            }
        }


        // =========================================================
        // WPF RESOURCE HELPERS
        // =========================================================

        private void SetAnimatedVisibility(UIElement element, Visibility targetVisibility, Action? afterHidden = null)
        {
            // Avoid repeatedly restarting an animation when the requested
            // visibility has not changed.
            if (visibilityAnimationTargets.TryGetValue(
                    element,
                    out Visibility previousTarget) &&
                previousTarget == targetVisibility)
            {
                return;
            }

            visibilityAnimationTargets[element] = targetVisibility;

            // Give every new transition its own generation number.
            int version = visibilityAnimationVersions.TryGetValue(
                element,
                out int previousVersion)
                    ? previousVersion + 1
                    : 1;

            visibilityAnimationVersions[element] = version;

            bool wasVisible = element.Visibility == Visibility.Visible;
            double currentOpacity = element.Opacity;

            // Stop any animation currently running on this element.
            element.BeginAnimation(UIElement.OpacityProperty, null);


            // =========================================================
            // FADE IN
            // =========================================================

            if (targetVisibility == Visibility.Visible)
            {
                element.Visibility = Visibility.Visible;

                if (ReferenceEquals(element, searchBorder) &&
    searchBorder.RenderTransform is TranslateTransform otherSlide)
                {
                    otherSlide.BeginAnimation(
                        TranslateTransform.YProperty,
                        new DoubleAnimation
                        {
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(160),
                            EasingFunction = new QuadraticEase
                            {
                                EasingMode = EasingMode.EaseOut
                            }
                        });
                }
                double fromOpacity = wasVisible
                    ? currentOpacity
                    : 0.0;

                element.Opacity = fromOpacity;

                if (fromOpacity >= 0.999)
                {
                    element.Opacity = 1.0;
                    return;
                }

                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = fromOpacity,
                    To = 1.0,
                    Duration = new Duration(
                        TimeSpan.FromMilliseconds(160)),

                    EasingFunction = new QuadraticEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                };

                fadeIn.Completed += (sender, args) =>
                {
                    if (!visibilityAnimationVersions.TryGetValue(
                            element,
                            out int activeVersion) ||
                        activeVersion != version)
                    {
                        return;
                    }

                    element.BeginAnimation(UIElement.OpacityProperty, null);
                    element.Opacity = 1.0;
                };

                element.BeginAnimation(
                    UIElement.OpacityProperty,
                    fadeIn,
                    HandoffBehavior.SnapshotAndReplace);

                return;
            }


            // =========================================================
            // IMMEDIATE HIDE IF ALREADY INVISIBLE
            // =========================================================

            if (!wasVisible)
            {
                element.Opacity = 1.0;
                element.Visibility = targetVisibility;

                afterHidden?.Invoke();
                return;
            }


            // =========================================================
            // FADE OUT
            // =========================================================

            element.Opacity = currentOpacity;

            if (ReferenceEquals(element, searchBorder) &&
    searchBorder.RenderTransform is TranslateTransform slide)
            {
                slide.BeginAnimation(
                    TranslateTransform.YProperty,
                    new DoubleAnimation
                    {
                        To = 14,
                        Duration = TimeSpan.FromMilliseconds(130),
                        EasingFunction = new QuadraticEase
                        {
                            EasingMode = EasingMode.EaseIn
                        }
                    });
            }

            DoubleAnimation fadeOut = new DoubleAnimation
            {
                From = currentOpacity,
                To = 0.0,
                Duration = new Duration(
                    TimeSpan.FromMilliseconds(130)),

                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            fadeOut.Completed += (sender, args) =>
            {
                // Ignore an outdated animation if the visibility has changed.
                if (!visibilityAnimationVersions.TryGetValue(
                        element,
                        out int activeVersion) ||
                    activeVersion != version)
                {
                    return;
                }

                element.BeginAnimation(UIElement.OpacityProperty, null);

                element.Opacity = 1.0;
                element.Visibility = targetVisibility;


                afterHidden?.Invoke();
            };

            element.BeginAnimation(
                UIElement.OpacityProperty,
                fadeOut,
                HandoffBehavior.SnapshotAndReplace);
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
