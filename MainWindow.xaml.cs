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
        private const double MaxVerseFontSize = 60;
        private const double MinVerseFontSize = 3;
        private readonly InputHandler inputHandler = new InputHandler();
        private List<string> testStrings = ["apple", "banana", "cherry"];
        private string backgroundImagePath = "images/dbw59.jpg";

        private bool globalUiVisible = true;
        private bool searchUiRequestedVisible = false;

        public MainWindow()
        {
            InitializeComponent();

            _database = BibleDatabase.Master();
            navigator = new BibleNavigator(_database);

            navigator.HelpRequested += OpenHelpWindow;
            navigator.CopyVerseText += CopyVerse;
            navigator.CopyVerseName += CopyTitle;
            navigator.DisplaySearchResults += DisplaySearch;
            navigator.FiltersHaveChanged += DisplayFilters;
            navigator.ChangeBackgroundPlease += ChangeBackgroundImage;

            inputHandler.SearchUiVisibilityChanged += SetSearchUiRequestedVisibility;
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

            // Search-related controls obey BOTH states.
            ApplySearchUiVisibility();
        }


        // Called when Linguist's search visibility request changes.
        private void SetSearchUiRequestedVisibility(bool visible)
        {
            searchUiRequestedVisible = visible;
            ApplySearchUiVisibility();
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

        private void ChangeBackgroundImage()
        {

            // Option B: Relative path if the image is set as Content / Copy if newer
            backgroundImagePath = backgroundImagePath switch
            {
                "images/dbw59.jpg" => "images/dbw02.jpg",
                "images/dbw02.jpg" => "images/dbw04.jpg",
                "images/dbw04.jpg" => "images/dbw08.jpg",
                "images/dbw08.jpg" => "images/dbw16.jpg",
                "images/dbw16.jpg" => "images/dbw17.jpg",
                "images/dbw17.jpg" => "images/dbw59.jpg",
                _ => "images/dbw59.jpg"
            };

            var uri = new Uri(backgroundImagePath, UriKind.Relative);

            var bitmap = new BitmapImage(uri);

            // Assign the new image source to the existing XAML brush
            BackgroundBrush.ImageSource = bitmap;

            // OR replace the window background entirely in code:
            // this.Background = new ImageBrush(bitmap);
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

        private void DisplaySearch()
        {
            List<Verse> SearchResults = navigator.LocatedVerses();
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