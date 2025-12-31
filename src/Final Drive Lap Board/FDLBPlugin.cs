
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using GameReaderCommon;
using SimHub.Plugins;

namespace Final_Drive_Lap_Board
{
    [PluginName("Final Drive Lap Board")]
    [PluginAuthor("Dirk Van Echelpoel")]
    [PluginDescription("Top Gear–style lap board for any supported sim.")]
    public class FDLBPlugin : IPlugin, IDataPlugin, IWPFSettings
    {
        private PluginManager _pluginManager;
        private readonly LapAttemptEngine _engine;
        private readonly TelemetryReader _telemetry;
        private readonly CarTrackCatalog _catalog;

        public FDLBPlugin()
        {
            _engine = new LapAttemptEngine();
            _telemetry = new TelemetryReader();
            _catalog = new CarTrackCatalog(GetCatalogPath());
        }

        public PluginManager PluginManager
        {
            get => _pluginManager;
            set => _pluginManager = value;
        }

        // --- Exposed to UI ---

        public string LiveLapText
        {
            get
            {
                var snapshot = _engine.GetSnapshot();
                string lapPart;

                if (snapshot.IsActive)
                {
                    int currentLapIndex = snapshot.Laps.Length + 1;
                    if (currentLapIndex < 1) currentLapIndex = 1;
                    lapPart = $"Lap {currentLapIndex}/{snapshot.LapsPerSet}";
                }
                else
                {
                    lapPart = "No attempt active";
                }

                double cur = _telemetry.CurrentLapTimeSeconds;
                if (cur > 0.0)
                {
                    var ts = TimeSpan.FromSeconds(cur);
                    string t = LapAttemptEngine.FormatLapTimeTenths(ts);
                    return $"{lapPart} – {t}";
                }

                return lapPart;
            }
        }

        public string BoardText => _engine.BoardText;

        public string[] CatalogCars => _catalog.Cars.ToArray();
        public string[] CatalogTracks => _catalog.Tracks.ToArray();

        // --- IPlugin / IDataPlugin ---

        public void Init(PluginManager pluginManager)
        {
            _pluginManager = pluginManager;
            _catalog.LoadOrCreateDefault();
        }

        public void End(PluginManager pluginManager)
        {
            try { _catalog.Save(); } catch { }
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            _telemetry.Tick(pluginManager, ref data, _engine);
        }

        // --- IWPFSettings ---

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new FDLBSettingsControl(this);
        }

        public void SaveWPFSettings(PluginManager pluginManager)
        {
            // All state is in text files (catalog + CSV exports).
        }

        // --- Called from UI ---

        public void StartNewAttempt(string driver, string car, string track, int lapsPerSet, string condition)
        {
            driver = driver ?? string.Empty;
            car = car ?? string.Empty;
            track = track ?? string.Empty;
            condition = string.IsNullOrWhiteSpace(condition) ? "Dry" : condition;

            // Auto-fill from telemetry if blanks
            if (string.IsNullOrWhiteSpace(car) && !string.IsNullOrWhiteSpace(_telemetry.LastCarName))
            {
                car = _telemetry.LastCarName;
            }

            if (string.IsNullOrWhiteSpace(track) && !string.IsNullOrWhiteSpace(_telemetry.LastTrackName))
            {
                track = _telemetry.LastTrackName;
            }

            _engine.ConfigureSession(driver, car, track, lapsPerSet, condition);
            _engine.StartAttempt();

            if (!string.IsNullOrWhiteSpace(car))
                _catalog.RememberCar(car);
            if (!string.IsNullOrWhiteSpace(track))
                _catalog.RememberTrack(track);

            try { _catalog.Save(); } catch { }

            _telemetry.ResetLapCounter();
        }

        public void AbortAttempt()
        {
            _engine.AbortAttempt();
        }

        public void CommitAttempt()
        {
            _engine.CommitAttempt();
        }

        public void ResetBoard()
        {
            _engine.ResetBoard();
        }

        public AttemptSnapshot GetAttemptSnapshot()
        {
            return _engine.GetSnapshot();
        }

        public void SetLapValidity(int attemptIndex, LapValidityTriState validity)
        {
            _engine.SetLapValidity(attemptIndex, validity);
        }

        public void ExportAttemptLaps()
        {
            string dir = EnsureOutputDir();
            string path = Path.Combine(
                dir,
                "LapAttempts_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".csv");
            _engine.SaveAttemptCsv(path);
        }

        public void ExportBoard()
        {
            string dir = EnsureOutputDir();
            string path = Path.Combine(
                dir,
                "LapBoard_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".csv");
            _engine.ExportBoardCsv(path);
        }

        // --- Helpers ---

        private string EnsureOutputDir()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dir = Path.Combine(docs, "SimHub", "Final Drive Lap Board");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetCatalogPath()
        {
            string dir = EnsureOutputDir();
            return Path.Combine(dir, "cars_tracks.ini");
        }
    }
}
