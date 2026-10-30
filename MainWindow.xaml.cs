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

        private bool globalUiVisible = true;
        private bool searchUiRequestedVisible = false;
        private bool marksVisible = false;

        public MainWindow()
        {
            InitializeComponent();

            SettingsGuy settingsGuy = new SettingsGuy();
            plumber = settingsGuy.loAd();

            backgroundImagePath = plumber.LastImageFilePath;

            BackgroundBrush.ImageSource = new BitmapImage(
                new Uri(backgroundImagePath, UriKind.Relative));

            _database = BibleDatabase.Master(plumber.LastTranslationNumber);
            navigator = new BibleNavigator(_database, plumber.LastMarks, plumber.LastBookNumber, plumber.LastChapterNumber, plumber.LastVerseNumber);

            navigator.HelpRequested += OpenHelpWindow;
            navigator.CopyVerseText += CopyVerse;
            navigator.CopyVerseName += CopyTitle;
            navigator.DisplaySearchResults += DisplaySearch;
            navigator.FiltersHaveChanged += DisplayFilters;
            navigator.ChangeBackgroundPlease += ChangeBackgroundImage;
            navigator.CopyBothTitleAndText += CopyAll;

            InputHandler.ExitApplication += CloseApplication;
            inputHandler.SearchUiVisibilityChanged += SetSearchUiRequestedVisibility;
            inputHandler.ShowMarks += ShowMarks;
            inputHandler.HideTheEnterBox += HideEnterBox;
            navigator.SearchUiVisibilityChanged += SetGlobalUiVisibility;
            navigator.PressEnterVisibilityChange += PressEnterSearchVisibility;

            navigator.UserScrolledSomewhere += ScrollRightList;

            modeBox.Text = inputHandler.GetStateText();

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            UpdateVerseDisplay();
        }

        private void ToggleUI()
        {
            globalUiVisible = !globalUiVisible;

            Visibility visibility = globalUiVisible
                ? Visibility.Visible
                : Visibility.Hidden;

            modeBox.Visibility = visibility;
            bufferBox.Visibility = visibility;
            hintBoxOne.Visibility = visibility;
            hintBoxTwo.Visibility = visibility;
            frequencyBox.Visibility = visibility;
            aliasBox.Visibility = visibility;
            refreshSearchBox.Visibility = visibility;

            // Search-related controls obey BOTH states.
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

        private void HideEnterBox()
        {
            refreshSearchBox.Visibility = Visibility.Hidden;
        }


        // Called when Navigator changes global UI visibility.
        private void SetGlobalUiVisibility(bool visible)
        {
            globalUiVisible = visible;

            // Replace this with your existing global UI
            // visibility logic for your ordinary controls.
            ToggleUI();

            // Search controls are handled separately below.
            ApplySearchUiVisibility();
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
            //refreshSearchBox.Visibility = visibility;

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
            DisplaySearch(State.V);
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
            modeBox.Text = inputHandler.GetStateText();
            bufferBox.Text = inputHandler.CommandInfo();

            if (grammaticalStatus == EntireCommandStatus.Valid)
            {
                Command? command = inputHandler.GetCommand();
                if (command != null)
                {
                    //if linguist says the command is valid, navigator has the final say about the validity of the command
                    navigator.ProcessCommand(command);
                    Color navigatorColor = navigator.CommandColor();
                    bufferBox.Background = new SolidColorBrush(navigatorColor);
                    UpdateVerseDisplay();
                    e.Handled = true;
                    return;
                }
            }
         
            //if linguist says the command is not valid, or it's an internal linguist command (state switch), linguist has the final say about the validity of the command
            Color linguistColor = inputHandler.CommandColor();
            bufferBox.Background = new SolidColorBrush(linguistColor);
            
            e.Handled = true;
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
            if (state == State.Choice)
            {
                List<Verse> SearchResults = navigator.LocatedVerses();
                rightList.ItemTemplate = (DataTemplate)FindResource("SearchVerseTemplate");
                rightList.ItemsSource = SearchResults;
                int frequency = SearchResults.Count;
                string frequencyText;
                if (frequency > 0)
                {
                    frequencyText = "\"" + navigator.ActualSearchQuery() + "\" : " + frequency.ToString() + " results found";
                    frequencyBox.Text = frequencyText;
                }
                else if (frequency == 1)
                {
                    frequencyText = "\"" + navigator.ActualSearchQuery() + "\" : " + frequency.ToString() + " result found";
                    frequencyBox.Text = frequencyText;
                }
                else
                {
                    frequencyText = "\"" + navigator.ActualSearchQuery() + "\" : " + "No result found";
                    frequencyBox.Text = frequencyText;
                }
            }
            else
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
    }
}