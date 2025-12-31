
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Final_Drive_Lap_Board
{
    public partial class FDLBSettingsControl : UserControl
    {
        private readonly FDLBPlugin _plugin;
        private readonly DispatcherTimer _timer;

        private sealed class LapListItem
        {
            public int AttemptIndex { get; set; }
            public string DisplayText { get; set; }
            public bool? IsChecked { get; set; }
        }

        public FDLBSettingsControl(FDLBPlugin plugin)
        {
            InitializeComponent();

            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

            CarComboBox.ItemsSource = _plugin.CatalogCars;
            TrackComboBox.ItemsSource = _plugin.CatalogTracks;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += (s, e) => RefreshFromPlugin();
            _timer.Start();

            Unloaded += (s, e) => _timer.Stop();
        }

        private void RefreshFromPlugin()
        {
            LiveLapLabel.Text = _plugin.LiveLapText ?? string.Empty;
            BoardTextBox.Text = _plugin.BoardText ?? string.Empty;

            var snapshot = _plugin.GetAttemptSnapshot();
            var items = new List<LapListItem>();

            foreach (var lap in snapshot.Laps)
            {
                bool? isChecked;
                string marker;

                switch (lap.Validity)
                {
                    case LapValidityTriState.Valid:
                        isChecked = true;
                        marker = "[V]";
                        break;
                    case LapValidityTriState.Invalid:
                        isChecked = false;
                        marker = "[X]";
                        break;
                    default:
                        isChecked = null;
                        marker = "[?]";
                        break;
                }

                string time = LapAttemptEngine.FormatLapTimeTenths(lap.LapTime);
                string display = $"#{lap.DisplayIndex}  {time}  {marker}";

                items.Add(new LapListItem
                {
                    AttemptIndex = lap.AttemptIndex,
                    DisplayText = display,
                    IsChecked = isChecked
                });
            }

            AttemptLapListView.ItemsSource = items;
        }

        private string GetSelectedCondition()
        {
            if (ConditionComboBox.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                return item.Content.ToString();
            }
            return "Dry";
        }

        private void StartAttempts_Click(object sender, RoutedEventArgs e)
        {
            int lapsPerSet = 3;
            int.TryParse(LapsPerSetTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out lapsPerSet);

            string driver = DriverTextBox.Text ?? string.Empty;
            string car = CarComboBox.Text ?? string.Empty;
            string track = TrackComboBox.Text ?? string.Empty;
            string condition = GetSelectedCondition();

            _plugin.StartNewAttempt(driver, car, track, lapsPerSet, condition);

            // Refresh catalog-based dropdowns in case new items were added.
            CarComboBox.ItemsSource = _plugin.CatalogCars;
            TrackComboBox.ItemsSource = _plugin.CatalogTracks;
        }

        private void AbortAttempts_Click(object sender, RoutedEventArgs e)
        {
            _plugin.AbortAttempt();
        }

        private void CommitAttempts_Click(object sender, RoutedEventArgs e)
        {
            _plugin.CommitAttempt();
        }

        private void SaveAttemptCsv_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ExportAttemptLaps();
        }

        private void ExportBoardCsv_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ExportBoard();
        }

        private void ResetBoard_Click(object sender, RoutedEventArgs e)
        {
            _plugin.ResetBoard();
        }

        private void LapValidityCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is LapListItem item)
            {
                LapValidityTriState validity;

                if (cb.IsChecked == true)
                    validity = LapValidityTriState.Valid;
                else if (cb.IsChecked == false)
                    validity = LapValidityTriState.Invalid;
                else
                    validity = LapValidityTriState.Unknown;

                _plugin.SetLapValidity(item.AttemptIndex, validity);
            }
        }
    }
}
