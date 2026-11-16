using System;
using System.Collections.Generic;
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

        private const double MaxVerseFontSize = 70;
        private const double MinVerseFontSize = 5;

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

        private AppSettings plumber = null!;
        private string backgroundImagePath = "images/dbw59.jpg";
        private Layout layout = Layout.Classic;

        private bool globalUiVisible = true;
        private bool searchUiRequestedVisible;
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

        // =========================================================
        // CONSTRUCTION AND LIFECYCLE
        // =========================================================

        public MainWindow()
        {
            InitializeComponent();

            SettingsGuy settingsGuy = new SettingsGuy();
            plumber = settingsGuy.loAd();

            backgroundImagePath = plumber.LastImageFilePath;
            BackgroundBrush.ImageSource = new BitmapImage(
                new Uri(backgroundImagePath, UriKind.Relative));

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
                plumber.LastVerseNumber, _aliasCatalog);

            ConnectNavigatorEvents();
            ConnectInputEvents();

            layout = plumber.LastPreferredLayout;

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Loaded += MainWindow_Loaded;
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

            SettingsGuy settingsGuy = new SettingsGuy();
            settingsGuy.saVe(plumber);
            settingsSaved = true;
        }

        // =========================================================
        // EVENT-DRIVEN INPUT
        // =========================================================

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            EntireCommandStatus status = inputHandler.ProcessKey(e.Key);

            if (status == EntireCommandStatus.Valid)
            {
                Command? command = inputHandler.GetCommand();

                if (command != null)
                {
                    // Linguist validates the command's grammar first. The
                    // navigator then decides whether it is valid in context.
                    navigator.ProcessCommand(command);
                    InterfaceUpdate(commandWasProcessedByNavigator: true);
                    UpdateVerseDisplay();
                    e.Handled = true;
                    return;
                }
            }

            InterfaceUpdate(commandWasProcessedByNavigator: false);
            e.Handled = true;
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
                _ => null
            };

            if (target == null)
                return;

            inputHandler.SelectNavigationMode(target.Value);

            // Refresh only the interface. No BibleNavigator command is needed.
            InterfaceUpdate(commandWasProcessedByNavigator: false);

            e.Handled = true;
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

            bottomList.Visibility = visibility;
            frequencyBox.Visibility = visibility;

            refreshSearchBox.Visibility =
                globalUiVisible && searchUiRequestedVisible && enterVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

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
            else
            {
                rightList.SelectedItem = null;
                rightList.ItemsSource = null;
            }

            ApplyRightListVisibility();
        }

        private void UpdateAliasSuggestions()
        {
            // Don't interfere with other uses of the right-hand list.
            // Also avoid generating suggestions when the entire UI is hidden.
            if (!globalUiVisible || marksVisible || searchUiRequestedVisible)
                return;

            string? prefix = inputHandler.AliasSuggestionPrefix();

            // No valid alias is currently being typed.
            if (prefix == null)
            {
                rightList.SelectedItem = null;
                rightList.ItemsSource = null;
                return;
            }

            // Display the alias suggestion template.
            rightList.ItemTemplate =
                (DataTemplate)FindResource("AliasSuggestionTemplate");

            // Retrieve only suggestions matching the user's prefix.
            rightList.ItemsSource =
                navigator.GetAliasSuggestions(prefix);
        }

        private void ApplyRightListVisibility()
        {
            bool shouldBeVisible =
                globalUiVisible &&
                (
                    searchUiRequestedVisible ||
                    marksVisible ||
                    inputHandler.AliasSuggestionPrefix() != null
                );

            rightList.Visibility = shouldBeVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                    bottomListBorder.BorderBrush = activeBorderBrush;
                    break;

                case State.Choice:
                    rightListBorder.BorderBrush = activeBorderBrush;
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
                    guidedLabel.Visibility = Visibility.Collapsed;
                    searchBorder.Visibility = Visibility.Collapsed;
                    break;

                case State.C:
                    bsBox.Text = string.Empty;
                    csBox.Text = commandInfo;
                    vsBox.Text = string.Empty;
                    guidedLabel.Text = string.Empty;

                    SetGuidedModeAppearance(
                        State.C,
                        GetCommandBrush(commandWasProcessedByNavigator, classic: false));
                    guidedLabel.Visibility = Visibility.Collapsed;
                    searchBorder.Visibility = Visibility.Collapsed;
                    break;

                case State.V:
                    bsBox.Text = string.Empty;
                    csBox.Text = string.Empty;
                    vsBox.Text = commandInfo;
                    guidedLabel.Text = string.Empty;

                    SetGuidedModeAppearance(
                        State.V,
                        GetCommandBrush(commandWasProcessedByNavigator, classic: false));
                    guidedLabel.Visibility = Visibility.Collapsed;
                    searchBorder.Visibility = Visibility.Collapsed;
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

            guidedLabel.Visibility = visibility;
            searchBorder.Visibility = visibility;

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
                    bottomListBorder.BorderBrush = activeBorderBrush;
                    break;

                case State.Choice:
                    rightListBorder.BorderBrush = activeBorderBrush;
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

            verseBox.FontSize = low;
            lastMeasuredVerseWidth = availableWidth;
            lastMeasuredVerseHeight = availableHeight;
        }

        // =========================================================
        // SEARCH RESULTS AND FILTERS
        // =========================================================

        private void DisplaySearch(State state)
        {
            List<Verse> searchResults = navigator.LocatedVerses();

            rightList.ItemTemplate =
                (DataTemplate)FindResource("SearchVerseTemplate");
            rightList.ItemsSource = searchResults;

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
            bottomList.ItemsSource = navigator.GetActiveSearchFilters();
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
            HelpWindow helpWindow = new HelpWindow();
            helpWindow.ShowDialog();
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

        // =========================================================
        // WPF RESOURCE HELPERS
        // =========================================================

        private static Brush CreateFrozenBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}