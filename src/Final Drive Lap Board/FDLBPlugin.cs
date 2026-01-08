using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows.Controls;
using GameReaderCommon;
using SimHub.Plugins;

namespace Final_Drive_Lap_Board
{
    [PluginName("Final Drive Lap Board")]
    [PluginAuthor("Dirk Van Echelpoel")]
    [PluginDescription("Top Gear–style lap board for any SimHub supported sim.")]
    public class FDLBPlugin : IPlugin, IDataPlugin, IWPFSettings
    {
        private PluginManager _pluginManager;
        private readonly LapAttemptEngine _engine;
        private readonly TelemetryReader _telemetry;
        private readonly CarTrackCatalog _catalog;
        private readonly Mcp4HLapEmitter _mcp4hEmitter;

        // Background poll timer to keep telemetry ticking even when SimHub pauses DataUpdate while switching tabs
        private System.Timers.Timer _pollTimer;
        private const double PollIntervalMs = 50;
        private readonly object _tickSync = new object();

        public FDLBPlugin()
        {
            _engine = new LapAttemptEngine();
            _telemetry = new TelemetryReader();
            _catalog = new CarTrackCatalog(GetCatalogPath());
            _mcp4hEmitter = new Mcp4HLapEmitter(GetMcp4hLogPath());
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

                // Lap number: prefer telemetry (actual lap counter), fallback to attempt index when active
                int lapNum = _telemetry.CurrentLapNumber > 0
                    ? _telemetry.CurrentLapNumber
                    : (snapshot.IsActive ? (snapshot.Laps.Length + 1) : 0);

                string track = DisplayTrack;
                string car = DisplayCar;
                string driver = DisplayDriver;
                string conditions = DisplayConditions;

                string head = snapshot.IsActive ? $"Lap {lapNum}" : "No attempt active";

                double cur = _telemetry.CurrentLapTimeSeconds;
                if (cur > 0.0)
                {
                    var ts = TimeSpan.FromSeconds(cur);
                    string t = LapAttemptEngine.FormatLapTimeHundredths(ts);
                    return $"{head} - {track} - {car} - {driver}  {t}  {conditions}".Trim();
                }

                return $"{head} - {track} - {car} - {driver}  {conditions}".Trim();
            }
        }

        public string DisplayDriver => _engine.DriverName ?? string.Empty;

        public string DisplayCar
        {
            get
            {
                var v = _engine.CarName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(v)) v = _telemetry.LastCarName ?? string.Empty;
                return v;
            }
        }

        public string DisplayTrack
        {
            get
            {
                var v = _engine.TrackName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(v)) v = _telemetry.LastTrackName ?? string.Empty;
                return v;
            }
        }

        public string DisplayConditions
        {
            get
            {
                var v = _engine.Conditions ?? "Dry";
                return string.IsNullOrWhiteSpace(v) ? "Dry" : v;
            }
        }

        public string BoardText => _engine.BoardText;
        public string OutputDirectory => EnsureOutputDir();
        public string[] CatalogCars => _catalog.Cars.ToArray();
        public string[] CatalogTracks => _catalog.Tracks.ToArray();
        public string DebugTelemetryText => _telemetry.DebugText ?? string.Empty;


        // --- IPlugin / IDataPlugin ---

        public void Init(PluginManager pluginManager)
        {
            _pluginManager = pluginManager;
            _catalog.LoadOrCreateDefault();
            _engine.LoadBoard(GetBoardPersistencePath());

            // Start background polling so tracking continues across SimHub UI tab switches
            StartPollTimer();
        }

        public void End(PluginManager pluginManager)
        {
            try
            {
                StopPollTimer();
                _engine.SaveBoard(GetBoardPersistencePath());
            }
            catch { }

            //try { _catalog.Save(); } catch { }
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Primary tick path when SimHub is actively feeding data
            _pluginManager = pluginManager;

            try
            {
                lock (_tickSync)
                {
                    _telemetry.Tick(pluginManager, ref data, _engine);
                }
            }
            catch { }
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
        public void ReloadCatalog()
        {
            _catalog.Reload();
        }

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
            var result = _engine.CommitAttempt();
            if (result != null && result.WasCommitted)
            {
                _mcp4hEmitter.AppendLapResult(result);
                _engine.SaveBoard(GetBoardPersistencePath());
            }
        }

        public void ResetBoard()
        {
            _engine.ResetBoard();
            _engine.SaveBoard(GetBoardPersistencePath());
        }

        public AttemptSnapshot GetAttemptSnapshot()
        {
            return _engine.GetSnapshot();
        }

        public void SetLapValidity(int attemptIndex, LapValidityTriState validity)
        {
            _engine.SetLapValidity(attemptIndex, validity);
        }

        public void UpdateConditions(string conditions)
        {
            _engine.Conditions = string.IsNullOrWhiteSpace(conditions) ? "Dry" : conditions;
        }
        
        public string ExportAttemptLaps()
        {
            string dir = EnsureOutputDir();
            string path = Path.Combine(
                dir,
                "LapAttempts_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".csv");
            _engine.SaveAttemptCsv(path);
            return path;
        }

        public string ExportBoard()
        {
            string dir = EnsureOutputDir();
            string path = Path.Combine(
                dir,
                "LapBoard_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".csv");
            _engine.ExportBoardCsv(path);
            return path;
        }


        private void StartPollTimer()
        {
            StopPollTimer();

            _pollTimer = new System.Timers.Timer(PollIntervalMs);
            _pollTimer.AutoReset = true;
            _pollTimer.Elapsed += (s, e) =>
            {
                try
                {
                    var pm = _pluginManager;
                    if (pm == null) return;

                    lock (_tickSync)
                    {
                        // Avoid double ticking when DataUpdate is flowing
                        if ((DateTime.UtcNow - _telemetry.LastTickUtc).TotalMilliseconds < 40) return;
                        _telemetry.Tick(pm, _engine);
                    }
                }
                catch { }
            };
            _pollTimer.Start();
        }

        private void StopPollTimer()
        {
            try
            {
                if (_pollTimer != null)
                {
                    _pollTimer.Stop();
                    _pollTimer.Dispose();
                    _pollTimer = null;
                }
            }
            catch { }
        }


        // --- Helpers ---
        public string DataFolderPath => EnsureOutputDir();

        public void OpenDataFolder()
        {
            try
            {
                var dir = EnsureOutputDir();
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch { /* swallow */ }
        }

        private string GetBoardPersistencePath()
        {
            // Keep it aligned with what you already use in that folder
            string dir = EnsureOutputDir();
            return System.IO.Path.Combine(dir, "powerlap_times.json");
        }


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

        private string GetBoardPath()
        {
            return Path.Combine(EnsureOutputDir(), "powerlap_times.json");
        }

        private string GetMcp4hLogPath()
        {
            string dir = EnsureOutputDir();
            return Path.Combine(dir, "FDLB_MCP4H_lapresults.jsonl");
        }
    }
}