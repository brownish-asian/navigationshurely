using BibleApp.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using navigationshurely.models;
using navigationshurely.Services;
using navigationshurely.services;
using System.Diagnostics;
using System;

namespace navigationshurely
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Verse verse;
        private readonly BibleNavigator navigator;
        private readonly BibleDatabase _database;
        private const double MaxVerseFontSize = 70;
        private const double MinVerseFontSize = 5;
        private readonly InputHandler inputHandler = new InputHandler();
        private List<string> testStrings = ["apple", "banana", "cherry"];
        private string backgroundImagePath = "images/dbw59.jpg";
        private AppSettings plumber;
        private Layout layout = Layout.Classic;
        private readonly Brush normalBackgroundColor;
        private readonly Brush normalBorderBrush;
        private readonly Brush transparentBrush;

        private bool globalUiVisible = true;
        private bool searchUiRequestedVisible = false;
        private bool marksVisible = false;
        private bool enterVisible = false;

        public MainWindow()
        {
            InitializeComponent();

            SettingsGuy settingsGuy = new SettingsGuy();
            plumber = settingsGuy.loAd();

            backgroundImagePath = plumber.LastImageFilePath;

            BackgroundBrush.ImageSource = new BitmapImage(
                new Uri(backgroundImagePath, UriKind.Relative));
            var converter = new BrushConverter();
            normalBackgroundColor = (Brush)converter.ConvertFrom("#55071320");
            normalBorderBrush = (Brush)converter.ConvertFrom("#68788C");
            transparentBrush = (Brush)converter.ConvertFrom("#00FFFFFF");
            _database = BibleDatabase.Master(plumber.LastTranslationNumber);
            navigator = new BibleNavigator(_database, plumber.LastMarks, plumber.LastBookNumber, plumber.LastChapterNumber, plumber.LastVerseNumber);

            navigator.HelpRequested += OpenHelpWindow;
            navigator.CopyVerseText += CopyVerse;
            navigator.CopyVerseName += CopyTitle;
            navigator.DisplaySearchResults += DisplaySearch;
            navigator.FiltersHaveChanged += DisplayFilters;
            navigator.ChangeBackgroundPlease += ChangeBackgroundImage;
            navigator.CopyBothTitleAndText += CopyAll;
            navigator.InterfaceChange += SwitchLayouts;

            InputHandler.ExitApplication += CloseApplication;
            inputHandler.SearchUiVisibilityChanged += SetSearchUiRequestedVisibility;
            inputHandler.ShowMarks += ShowMarks;
            inputHandler.HideTheEnterBoxOrNah += ToggleEnterBox;
            navigator.SearchUiVisibilityChanged += SetGlobalUiVisibility;
            navigator.PressEnterVisibilityChange += PressEnterSearchVisibility;

            navigator.UserScrolledSomewhere += ScrollRightList;

            layout = plumber.LastPreferredLayout;

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Loaded += (sender, e) =>
            {
                UpdateVerseDisplay();
                if (layout is Layout.Classic)
                {
                    showClassic(Visibility.Visible);
                }
                else if (layout is Layout.Guided)
                {
                    showGuided(Visibility.Visible);
                }
                InterfaceUpdate(false);
            };
        }

        private void SwitchLayouts()
        {
            if (layout is Layout.Classic)
            {
                layout = Layout.Guided;
                showGuided(Visibility.Visible);
            }

            else if (layout is Layout.Guided)
            {
                layout = Layout.Classic;
                showClassic(Visibility.Visible);
            }

        }

        private void showGuided(Visibility visibility)
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

        private void showClassic(Visibility visibility)
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

        private void ToggleUI()
        {
            globalUiVisible = !globalUiVisible;

            Visibility visibility = globalUiVisible
                ? Visibility.Visible
                : Visibility.Hidden;

            aliasBox.Visibility = visibility;
             
            if (layout is Layout.Classic)
            {
                showClassic(visibility);
            }
            else if (layout is Layout.Guided)
            {
                showGuided(visibility);
            }
            ApplySearchUiVisibility();
        }

        protected override void OnClosed(EventArgs e)
        {
            PrepareToClose();
            base.OnClosed(e);
        }

        // Called when Linguist's search visibility request changes.
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

        private void ToggleEnterBox(bool visible)
        {
            enterVisible = visible;
            ApplySearchUiVisibility();
        }


        // Called when Navigator changes global UI visibility.
        private void SetGlobalUiVisibility(bool visible)
        {
            globalUiVisible = visible;

            // Replace this with your existing global UI
            // visibility logic for your ordinary controls.
            ToggleUI();
        }

        private void ApplySearchUiVisibility()
        {

            Visibility visibility =
                globalUiVisible && searchUiRequestedVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Replace these example names with your actual
            // search UI controls.
            rightList.Visibility = visibility;
            bottomList.Visibility = visibility;
            frequencyBox.Visibility = visibility;
            refreshSearchBox.Visibility = globalUiVisible && enterVisible ? Visibility.Visible : Visibility.Collapsed;

            if (searchUiRequestedVisible)
            {
                hintBoxTwo.Text = "";
                hintBoxOne.Text = "Press '/' again to exit Search Mode";
            }
            else
            {
                hintBoxOne.Text = "[space];f for help";
                hintBoxTwo.Text = "[space];e to toggle UI";
            }
        }

        private void ApplyMarksVisibility()
        {
            Visibility visibility = globalUiVisible && marksVisible? Visibility.Visible : Visibility.Collapsed;

            rightList.Visibility = visibility; 
            if (marksVisible)
            {
                rightList.ItemTemplate = (DataTemplate)FindResource("MarkedVerseTemplate");

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

        }

        private void CloseApplication()
        {
            PrepareToClose();
            Application.Current.Shutdown();
        }

        private void PrepareToClose()
        {
            plumber.LastTranslationNumber = _database.TranslationNumber();
            plumber.LastMarks = navigator.BookMarks();
            plumber.LastBookNumber = verse.BookId;
            plumber.LastChapterNumber = verse.Chapter;
            plumber.LastVerseNumber = verse.VerseNumber;
            plumber.LastImageFilePath = backgroundImagePath;
            plumber.LastPreferredLayout = layout;
            SettingsGuy settingsGuy = new SettingsGuy();
            settingsGuy.saVe(plumber);
        }

        private void PressEnterSearchVisibility(bool visible)
        {
            Visibility visibility = globalUiVisible && visible? Visibility.Visible : Visibility.Collapsed;
            refreshSearchBox.Visibility = visibility;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            EntireCommandStatus grammaticalStatus = inputHandler.ProcessKey(e.Key);

            if (grammaticalStatus == EntireCommandStatus.Valid)
            {
                Command? command = inputHandler.GetCommand();
                if (command != null)
                {
                    //if linguist says the command is valid, navigator has the final say about the validity of the command
                    navigator.ProcessCommand(command);
                    InterfaceUpdate(true);
                    UpdateVerseDisplay();
                    e.Handled = true;
                    return;
                }
            }
            InterfaceUpdate(false);
            e.Handled = true;
        }

        private void InterfaceUpdate(bool itWasExternal)
        {
            if (layout is Layout.Classic)
            {
                modeBox.Text = inputHandler.GetStateText();
                bufferBox.Text = inputHandler.CommandInfo();


                var hexColorS = (Color)ColorConverter.ConvertFromString("#E5B94F");

                State currentState = inputHandler.getState();
                switch (currentState)
                {
                    case State.Filter:
                    bottomListBorder.BorderBrush = new SolidColorBrush(hexColorS);

                        rightListBorder.BorderBrush = Brushes.Transparent;
                        bufferBoxBorder.BorderBrush = Brushes.Transparent;
                        break;

                    case State.Choice:
                    rightListBorder.BorderBrush = new SolidColorBrush(hexColorS);

                    bottomListBorder.BorderBrush = Brushes.Transparent;
                        bufferBoxBorder.BorderBrush = Brushes.Transparent;
                        break;

                    case State.Query:
                        bufferBoxBorder.BorderBrush = new SolidColorBrush(hexColorS);

                    rightListBorder.BorderBrush = Brushes.Transparent;
                        bottomListBorder.BorderBrush = Brushes.Transparent;
                        break;

                    default:
                        bufferBoxBorder.BorderBrush = transparentBrush;
                        rightListBorder.BorderBrush = Brushes.Transparent;
                        bottomListBorder.BorderBrush = Brushes.Transparent;
                        break;
                }
                if (itWasExternal)
                {
                    Color navigatorColor = navigator.CommandColor();
                    bufferBox.Background = new SolidColorBrush(navigatorColor);
                }
                else
                {
                    //if linguist says the command is not valid, or it's an internal linguist command (state switch), linguist has the final say about the validity of the command
                    Color linguistColor = inputHandler.CommandColor(true);
                    bufferBox.Background = new SolidColorBrush(linguistColor);
                }
            }
            else if (layout is Layout.Guided) 
            {
                verse = navigator.CurrentVerse;
                var (bookString, chapterString, verseString) =  GetVerseParts(verse);
                guidedBookBox.Text = bookString;
                guidedChapterBox.Text = chapterString;
                guidedVerseBox.Text = verseString;
                State currentState = inputHandler.getState();
                
                switch (currentState)
                {
                    case State.B:
                        bsBox.Text = inputHandler.CommandInfo();
                        csBox.Text = "";
                        vsBox.Text = "";
                        guidedLabel.Text = "";
                        if (itWasExternal)
                        {
                            Color navigatorColor = navigator.CommandColor();
                            bookModeIndicator.Background = new SolidColorBrush(navigatorColor);

                        }
                        else
                        {
                            //if linguist says the command is not valid, or it's an internal linguist command (state switch), linguist has the final say about the validity of the command
                            Color linguistColorB = inputHandler.CommandColor(false);
                            bookModeIndicator.Background = new SolidColorBrush(linguistColorB);

                        }
                        var hexColor = (Color)ColorConverter.ConvertFromString("#E5B94F");
                        bBorder.BorderBrush = new SolidColorBrush(hexColor);

                        chapterModeIndicator.Background = normalBackgroundColor;
                        cBorder.BorderBrush = normalBorderBrush;

                        verseModeIndicator.Background = normalBackgroundColor;
                        vBorder.BorderBrush = normalBorderBrush;

                        guidedLabel.Visibility = Visibility.Collapsed;
                        searchBorder.Visibility = Visibility.Collapsed;

                        break;
                    case State.C:
                        bsBox.Text = "";
                        csBox.Text = inputHandler.CommandInfo();
                        vsBox.Text = "";
                        guidedLabel.Text = "";

                        if (itWasExternal)
                        {
                            Color navigatorColor = navigator.CommandColor();
                            chapterModeIndicator.Background = new SolidColorBrush(navigatorColor);

                        }
                        else
                        {
                            Color linguistColorC = inputHandler.CommandColor(false);
                            chapterModeIndicator.Background = new SolidColorBrush(linguistColorC);

                        }

                        var hexColorC = (Color)ColorConverter.ConvertFromString("#E5B94F");
                        cBorder.BorderBrush = new SolidColorBrush(hexColorC);

                        bookModeIndicator.Background = normalBackgroundColor;
                        bBorder.BorderBrush = normalBorderBrush;

                        verseModeIndicator.Background = normalBackgroundColor;
                        vBorder.BorderBrush = normalBorderBrush;

                        guidedLabel.Visibility = Visibility.Collapsed;
                        searchBorder.Visibility = Visibility.Collapsed;

                        break;
                    case State.V:
                        bsBox.Text = "";
                        csBox.Text = "";
                        guidedLabel.Text = "";
                        vsBox.Text = inputHandler.CommandInfo();
                        if (itWasExternal)
                        {
                            Color navigatorColor = navigator.CommandColor();
                            verseModeIndicator.Background = new SolidColorBrush(navigatorColor);

                        }
                        else
                        {
                            //if linguist says the command is not valid, or it's an internal linguist command (state switch), linguist has the final say about the validity of the command
                            Color linguistColorV = inputHandler.CommandColor(false);
                            verseModeIndicator.Background = new SolidColorBrush(linguistColorV);

                        }
                        var hexColorV = (Color)ColorConverter.ConvertFromString("#E5B94F");
                        vBorder.BorderBrush = new SolidColorBrush(hexColorV);

                        bookModeIndicator.Background = normalBackgroundColor;
                        bBorder.BorderBrush = normalBorderBrush;

                        chapterModeIndicator.Background = normalBackgroundColor;
                        cBorder.BorderBrush = normalBorderBrush;

                        guidedLabel.Visibility = Visibility.Collapsed;
                        searchBorder.Visibility = Visibility.Collapsed;


                        break;

                    case State.S:
                    case State.Choice:
                    case State.Query:
                    case State.Filter:
                        Visibility visibility = globalUiVisible ? Visibility.Visible : Visibility.Collapsed;
                        guidedLabel.Visibility = visibility;
                        searchBorder.Visibility = visibility;
                        bsBox.Text = "";
                        csBox.Text = "";
                        vsBox.Text = "";

                        bookModeIndicator.Background = normalBackgroundColor;
                        bBorder.BorderBrush = normalBorderBrush;

                        chapterModeIndicator.Background = normalBackgroundColor;
                        cBorder.BorderBrush = normalBorderBrush;

                        verseModeIndicator.Background = normalBackgroundColor;
                        vBorder.BorderBrush = normalBorderBrush;

                        guidedLabel.Text = inputHandler.CommandInfo();
                        if (itWasExternal)
                        {
                            Color navigatorColor = navigator.CommandColor();
                            guidedLabel.Background = new SolidColorBrush(navigatorColor);
                        }
                        else
                        {
                            //if linguist says the command is not valid, or it's an internal linguist command (state switch), linguist has the final say about the validity of the command
                            Color linguistColorS = inputHandler.CommandColor(true);
                            guidedLabel.Background = new SolidColorBrush(linguistColorS);
                        }
                            var hexColorS = (Color)ColorConverter.ConvertFromString("#E5B94F");
                            if (currentState == State.Filter)
                            {
                                bottomListBorder.BorderBrush = new SolidColorBrush(hexColorS);

                                rightListBorder.BorderBrush = Brushes.Transparent;
                            searchBorder.BorderBrush = Brushes.Transparent;
                        }
                            else if (currentState == State.Choice) 
                            {
                                rightListBorder.BorderBrush = new SolidColorBrush(hexColorS);

                                bottomListBorder.BorderBrush = Brushes.Transparent;
                            searchBorder.BorderBrush = Brushes.Transparent;
                        }
                            else
                            {
                                searchBorder.BorderBrush = new SolidColorBrush(hexColorS);

                                rightListBorder.BorderBrush = Brushes.Transparent;
                            bottomListBorder.BorderBrush = Brushes.Transparent;
                            }

                        break;
                }
            }
        }

        private void UpdateVerseDisplay()
        {
            verse = navigator.CurrentVerse;
            titleBox.Text = verse.ToString();
            verseBox.Text = verse.Text;
            translationBox.Text = _database.TranslationName();
            aliasBox.Text = inputHandler.AliasForThisVerse(navigator.VerseEncodedNumber());
            AdjustVerseFontSize();
        }

        private void ChangeBackgroundImage(Direct direction)
        {
            string[] backgrounds =
            {
                "images/dbw59.jpg",
                "images/dbw02.jpg",
                "images/dbw04.jpg",
                "images/dbw08.jpg",
                "images/dbw16.jpg",
                "images/dbw17.jpg"
            };

            int index = Array.IndexOf(backgrounds, backgroundImagePath);

            if (index < 0)
                index = 0;

            int step = direction switch
            {
                Direct.Up => 1,
                Direct.Down => -1,
                _ => 0
            };

            index = (index + step + backgrounds.Length)
                % backgrounds.Length;

            backgroundImagePath = backgrounds[index];

            BackgroundBrush.ImageSource = new BitmapImage(
                new Uri(backgroundImagePath, UriKind.Relative));
        }

        private void OpenHelpWindow()
        {
            HelpWindow helpWindow = new HelpWindow();
            helpWindow.ShowDialog();
        }

        private void AdjustVerseFontSize()
        {
            if (verseBox == null || verseContainer == null)
                return;

            if (string.IsNullOrWhiteSpace(verseBox.Text))
                return;

            double availableWidth = verseContainer.ActualWidth;
            double availableHeight = verseContainer.ActualHeight;

            if (availableWidth <= 0 || availableHeight <= 0)
                return;

            double low = MinVerseFontSize;
            double high = MaxVerseFontSize;

            while (high - low > 1)
            {
                double middle = (low + high) / 2;

                verseBox.FontSize = middle;

                verseBox.Measure(new Size(
                    availableWidth,
                    double.PositiveInfinity
                ));

                if (verseBox.DesiredSize.Height <= availableHeight)
                    low = middle;
                else
                    high = middle;
            }

            verseBox.FontSize = low;
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
            string verseTitle = titleBox.Text;
            string verseContent = verseBox.Text;
            Clipboard.SetText(verseTitle + "\n" + verseContent);
        }

        private void DisplaySearch(State state)
        {
            List<Verse> SearchResults = navigator.LocatedVerses();
            rightList.ItemTemplate = (DataTemplate)FindResource("SearchVerseTemplate");
            rightList.ItemsSource = SearchResults;
            int frequency = SearchResults.Count;
            string frequencyText;
            if (frequency == 1)
            {
                frequencyText = "\"" + navigator.ActualSearchQuery() + "\" : " + frequency.ToString() + " result found";
                frequencyBox.Text = frequencyText;
            }
            else if (frequency > 1)
            {
                frequencyText = "\"" + navigator.ActualSearchQuery() + "\" : " + frequency.ToString() + " results found";
                frequencyBox.Text = frequencyText;
            }
            else
            {
                frequencyText = "\"" + navigator.ActualSearchQuery() + "\" : " + "No results found";
                frequencyBox.Text = frequencyText;
            }
        }

        private void DisplayFilters()
        {
            List<String> FilterResults = navigator.GetActiveSearchFilters();
            bottomList.ItemsSource = FilterResults;
        }

        private void ScrollRightList()
        {
            int selectedSearchIndex = navigator.SelectedSearchIndex();
            if (rightList.Items.Count > selectedSearchIndex)
            {
                //Verse verseForTesting = (Verse) rightList.Items[selectedSearchIndex];
                // Tell WPF to bring that verse into view.
                rightList.ScrollIntoView(rightList.Items[selectedSearchIndex]);
            }
        }

        private void rightList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DependencyObject clickedElement = e.OriginalSource as DependencyObject;

            ListViewItem clickedItem = FindParent<ListViewItem>(clickedElement);

            if (clickedItem != null)
            {
                object item = rightList.ItemContainerGenerator.ItemFromContainer(clickedItem);

                if (item is Verse clickedVerse)
                {
                    navigator.SetCurrentVerse(clickedVerse);
                    UpdateVerseDisplay();
                }
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private (string Book, string Chapter, string Verse) GetVerseParts(Verse verse)
        {
            // Retrieve all book names using the existing public Title() method.
            string[] titles = Enumerable.Range(1, 66)
                .Select(id => new Verse { BookId = id }.Title())
                .ToArray();

            // Remove spaces and punctuation to create searchable names.
            string[] names = titles
                .Select(name => new string(name.Where(char.IsLetterOrDigit).ToArray()))
                .ToArray();

            // Handle cases where one complete book name is a prefix of another.
            // Example: AWIT and AWIT NG MGA AWIT.
            for (int i = 0; i < names.Length; i++)
            {
                bool startsWithAnotherTitle = names
                    .Where((other, j) => j != i)
                    .Any(other =>
                        names[i].Length > other.Length &&
                        names[i].StartsWith(other, StringComparison.Ordinal));

                if (startsWithAnotherTitle)
                {
                    names[i] = string.Concat(
                        titles[i]
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => word[0])
                    );
                }
            }

            // Find the minimum number of characters needed to uniquely
            // identify every book.
            int codeLength = 1;

            for (; codeLength <= 5; codeLength++)
            {
                string[] codes = names
                    .Select(name => name[..Math.Min(codeLength, name.Length)])
                    .ToArray();

                if (codes.Distinct(StringComparer.Ordinal).Count() == 66)
                    break;
            }

            if (codeLength > 5)
                throw new InvalidOperationException("Could not uniquely identify every book.");

            // Extract the identifier for the requested verse.
            string book = names[verse.BookId - 1];

            string bookString = book[..Math.Min(codeLength, book.Length)];
            string chapterString = verse.Chapter.ToString();
            string verseString = verse.VerseNumber.ToString();

            return (bookString, chapterString, verseString);
        }
    }
}