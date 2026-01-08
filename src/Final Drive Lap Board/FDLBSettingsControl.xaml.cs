using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Diagnostics;   // <-- add
using System.IO;            // <-- add

namespace Final_Drive_Lap_Board
{
    public partial class FDLBSettingsControl : UserControl
    {
        private FDLBPlugin _plugin;
        private readonly DispatcherTimer _uiTimer;

        private readonly ObservableCollection<LapListItem> _lapItems = new ObservableCollection<LapListItem>();
        private bool _suppressValidityEvent;

        public FDLBSettingsControl()
        {
            InitializeComponent();

            AttemptLapListView.ItemsSource = _lapItems;

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _uiTimer.Tick += (s, e) => RefreshFromPlugin();

            Loaded += (s, e) =>
            {
                _plugin = DataContext as FDLBPlugin;
                _uiTimer.Start();
                RefreshFromPlugin();
            };

            Unloaded += (s, e) =>
            {
                _uiTimer.Stop();
            };
        }

        public FDLBSettingsControl(FDLBPlugin plugin) : this()
        {
            _plugin = plugin;
            DataContext = plugin;
        }

        private void RefreshFromPlugin()
        {
            if (_plugin == null) _plugin = DataContext as FDLBPlugin;
            if (_plugin == null) return;

            LiveLapLabel.Text = _plugin.LiveLapText ?? string.Empty;
            BoardTextBox.Text = _plugin.BoardText ?? string.Empty;
            DebugTextBox.Text = _plugin.DebugTelemetryText ?? string.Empty;

            // Populate dropdowns once (avoid UI jitter).
            CarComboBox.ItemsSource = _plugin.CatalogCars;
            TrackComboBox.ItemsSource = _plugin.CatalogTracks;

            var snapshot = _plugin.GetAttemptSnapshot();
            SyncAttemptLapList(snapshot);
        }
        private void ReloadCatalog_Click(object sender, RoutedEventArgs e)
        {
            if (_plugin == null) return;

            _plugin.ReloadCatalog();
            RefreshFromPlugin();
        }

        private void SyncAttemptLapList(AttemptSnapshot snapshot)
        {
            if (snapshot == null) return;

            // Metadata for display: use current UI selections
            string driver = (DriverTextBox.Text ?? string.Empty).Trim();
            string car = (CarComboBox.SelectedItem as string) ?? (CarComboBox.Text ?? string.Empty);
            string track = (TrackComboBox.SelectedItem as string) ?? (TrackComboBox.Text ?? string.Empty);
            string conditions = ((ConditionComboBox.SelectedItem as ComboBoxItem)?.Content as string) ?? (ConditionComboBox.Text ?? string.Empty);

            _suppressValidityEvent = true;
            try
            {
                // Work with a stable ordered array
                var laps = snapshot.Laps.OrderBy(l => l.AttemptIndex).ToArray();

                for (int i = 0; i < laps.Length; i++)
                {
                    var lap = laps[i];
                    LapListItem item;

                    if (i < _lapItems.Count)
                    {
                        // Reuse existing item – do NOT stomp its IsChecked
                        item = _lapItems[i];
                        item.AttemptIndex = lap.AttemptIndex;

                        // Initial engine validity only if UI has never set anything yet
                        if (item.IsChecked == null)
                        {
                            item.IsChecked = MapValidityToNullableBool(lap.Validity);
                        }
                    }
                    else
                    {
                        // New lap: create item with engine validity
                        item = new LapListItem
                        {
                            AttemptIndex = lap.AttemptIndex,
                            IsChecked = MapValidityToNullableBool(lap.Validity)
                        };
                        _lapItems.Add(item);
                    }

                    string time = LapAttemptEngine.FormatLapTimeHundredths(lap.LapTime);
                    string marker = ValidityMarkerForDisplay(lap.Validity);

                    item.DisplayText = $"[{marker}]  Lap {lap.DisplayIndex}  {track}  {car}  {driver}  {time}  {conditions}".Trim();
                }

                // Remove extra items if attempt has fewer laps than before
                while (_lapItems.Count > laps.Length)
                {
                    _lapItems.RemoveAt(_lapItems.Count - 1);
                }
            }
            finally
            {
                _suppressValidityEvent = false;
            }
        }

        private static bool? MapValidityToNullableBool(LapValidityTriState validity)
        {
            switch (validity)
            {
                case LapValidityTriState.Valid: return true;
                case LapValidityTriState.Invalid: return false;
                default: return null;
            }
        }
        private static string ValidityMarkerForDisplay(LapValidityTriState validity)
        {
            switch (validity)
            {
                case LapValidityTriState.Valid: return "V";
                case LapValidityTriState.Invalid: return "X";
                default: return "?";
            }
        }

        private void ConditionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_plugin == null) return;

            string conditions = ((ConditionComboBox.SelectedItem as ComboBoxItem)?.Content as string)
                                ?? (ConditionComboBox.Text ?? string.Empty);

            _plugin.UpdateConditions(conditions);
            RefreshFromPlugin();
        }

        private void StartAttempts_Click(object sender, RoutedEventArgs e)
        {
            if (_plugin == null) return;

            string driver = (DriverTextBox.Text ?? string.Empty).Trim();
            string car = (CarComboBox.SelectedItem as string) ?? (CarComboBox.Text ?? string.Empty);
            string track = (TrackComboBox.SelectedItem as string) ?? (TrackComboBox.Text ?? string.Empty);
            string conditions = ((ConditionComboBox.SelectedItem as ComboBoxItem)?.Content as string) ?? (ConditionComboBox.Text ?? string.Empty);

            int lapsPerSet = 3;
            int.TryParse(LapsPerSetTextBox.Text, out lapsPerSet);
            if (lapsPerSet <= 0) lapsPerSet = 3;

            _plugin.StartNewAttempt(driver, car, track, lapsPerSet, conditions);
            RefreshFromPlugin();
        }

        private void AbortAttempts_Click(object sender, RoutedEventArgs e)
        {
            _plugin?.AbortAttempt();
            RefreshFromPlugin();
        }

        private void CommitAttempts_Click(object sender, RoutedEventArgs e)
        {
            _plugin?.CommitAttempt();
            RefreshFromPlugin();
        }

        private void SaveAttemptCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_plugin == null) return;

            string path = _plugin.ExportAttemptLaps();
            RefreshFromPlugin();
            ShowExportMessage(path);
        }

        private void ExportBoardCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_plugin == null) return;

            string path = _plugin.ExportBoard();
            RefreshFromPlugin();
            ShowExportMessage(path);
        }

        private void ShowExportMessage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || _plugin == null)
                return;

            var result = MessageBox.Show(
                $"Saved to:\n{path}\n\nOpen folder?",
                "Final Drive Lap Board",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                // Use the plugin’s own helper so we always open the correct SimHub/FDLB folder
                _plugin.OpenDataFolder();
            }
        }

        private void ResetBoard_Click(object sender, RoutedEventArgs e)
        {
            _plugin?.ResetBoard();
            RefreshFromPlugin();
        }

        private void EditCatalog_Click(object sender, RoutedEventArgs e)
        {
            _plugin?.OpenDataFolder();
        }

        private void LapValidity_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressValidityEvent) return;
            if (_plugin == null) return;

            if (!(sender is CheckBox cb)) return;
            if (!(cb.DataContext is LapListItem item)) return;

            var validity = cb.IsChecked == true
                ? LapValidityTriState.Valid
                : cb.IsChecked == false
                    ? LapValidityTriState.Invalid
                    : LapValidityTriState.Unknown;

            _plugin.SetLapValidity(item.AttemptIndex, validity);
            RefreshFromPlugin();
        }

        private sealed class LapListItem : DependencyObject
        {
            public int AttemptIndex { get; set; }

            public bool? IsChecked
            {
                get { return (bool?)GetValue(IsCheckedProperty); }
                set { SetValue(IsCheckedProperty, value); }
            }

            public static readonly DependencyProperty IsCheckedProperty =
                DependencyProperty.Register("IsChecked", typeof(bool?), typeof(LapListItem), new PropertyMetadata(null));

            public string DisplayText
            {
                get { return (string)GetValue(DisplayTextProperty); }
                set { SetValue(DisplayTextProperty, value); }
            }

            public static readonly DependencyProperty DisplayTextProperty =
                DependencyProperty.Register("DisplayText", typeof(string), typeof(LapListItem), new PropertyMetadata(string.Empty));
        }
    }
}