using System;
using System.Globalization;
using GameReaderCommon;
using SimHub.Plugins;

namespace Final_Drive_Lap_Board
{
    /// <summary>
    /// Telemetry adapter: reads canonical SimHub DataCorePlugin.GameData.* properties (with NewData fallbacks),
    /// detects completed laps, and exposes current lap time + last car/track for UI/commit.
    /// </summary>
    public sealed class TelemetryReader
    {
        private int _lastCompletedLaps;

        public int TickCount { get; private set; }
        public DateTime LastTickUtc { get; private set; }

        public int CompletedLaps { get; private set; }
        public int CurrentLapNumber { get; private set; }

        public double CurrentLapTimeSeconds { get; private set; }
        public double LastLapTimeSeconds { get; private set; }

        public bool TelemetryValid { get; private set; }

        public string LastCarName { get; private set; } = string.Empty;
        public string LastTrackName { get; private set; } = string.Empty;

        public string DebugText { get; private set; } = string.Empty;

        public void ResetLapCounter()
        {
            _lastCompletedLaps = CompletedLaps;
        }

        public void Tick(PluginManager manager, ref GameData data, LapAttemptEngine engine)
        {
            if (manager == null || engine == null) return;

            TickCount++;
            LastTickUtc = DateTime.UtcNow;

            // Canonical SimHub properties live under DataCorePlugin.GameData.* (and sometimes GameData.NewData.*)
            int completedLaps = ReadInt(manager,
                "DataCorePlugin.GameData.CompletedLaps",
                "DataCorePlugin.GameData.NewData.CompletedLaps");

            int currentLap = ReadInt(manager,
                "DataCorePlugin.GameData.CurrentLap",
                "DataCorePlugin.GameData.NewData.CurrentLap",
                "DataCorePlugin.GameData.LapNumber",
                "DataCorePlugin.GameData.NewData.LapNumber");

            double currentLapTime = ReadSeconds(manager,
                "DataCorePlugin.GameData.CurrentLapTime",
                "DataCorePlugin.GameData.NewData.CurrentLapTime",
                "DataCorePlugin.GameData.LapTimeCurrent",
                "DataCorePlugin.GameData.NewData.LapTimeCurrent");

            double lastLapTime = ReadSeconds(manager,
                "DataCorePlugin.GameData.LastLapTime",
                "DataCorePlugin.GameData.NewData.LastLapTime",
                "DataCorePlugin.GameData.LastLapTimeAnyLap",
                "DataCorePlugin.GameData.NewData.LastLapTimeAnyLap");

            bool lapInvalidated = ReadBool(manager,
                "DataCorePlugin.GameData.LapInvalidated",
                "DataCorePlugin.GameData.NewData.LapInvalidated",
                "DataCorePlugin.GameData.LapInvalidatedByCut",
                "DataCorePlugin.GameData.NewData.LapInvalidatedByCut");

            string carName = ReadString(manager,
                "DataCorePlugin.GameData.CarModel",
                "DataCorePlugin.GameData.NewData.CarModel",
                "DataCorePlugin.GameData.CarName",
                "DataCorePlugin.GameData.NewData.CarName");

            string trackName = ReadString(manager,
                "DataCorePlugin.GameData.TrackName",
                "DataCorePlugin.GameData.NewData.TrackName");

            // Update exposed state
            CompletedLaps = completedLaps;
            CurrentLapNumber = currentLap;
            CurrentLapTimeSeconds = currentLapTime;
            LastLapTimeSeconds = lastLapTime;
            TelemetryValid = !lapInvalidated;

            if (!string.IsNullOrWhiteSpace(carName)) LastCarName = carName;
            if (!string.IsNullOrWhiteSpace(trackName)) LastTrackName = trackName;

            // Detect new completed lap
            if (completedLaps > _lastCompletedLaps)
            {
                _lastCompletedLaps = completedLaps;

                if (lastLapTime > 0.001)
                {
                    engine.OnLapCompleted(TimeSpan.FromSeconds(lastLapTime), TelemetryValid);
                }
            }

            DebugText = string.Format(CultureInfo.InvariantCulture,
                "ticks={0}  lastTickUtc={1:HH:mm:ss.fff}Z  lap={2}  completed={3}  cur={4:0.000}s  last={5:0.000}s  telemetryValid={6}  car='{7}'  track='{8}'",
                TickCount, LastTickUtc, CurrentLapNumber, CompletedLaps, CurrentLapTimeSeconds, LastLapTimeSeconds, TelemetryValid,
                LastCarName ?? string.Empty, LastTrackName ?? string.Empty);
        }


        /// <summary>
        /// Tick without GameData (used from background poll timer).
        /// Calls the ref-GameData Tick using a dummy GameData value; the implementation reads from PluginManager properties.
        /// </summary>
        public void Tick(PluginManager manager, LapAttemptEngine engine)
        {
            var dummy = new GameData();
            Tick(manager, ref dummy, engine);
        }



        private static object TryGet(PluginManager manager, string path)
        {
            try { return manager.GetPropertyValue(path); } catch { return null; }
        }

        private static int ReadInt(PluginManager manager, params string[] paths)
        {
            foreach (var p in paths)
            {
                var o = TryGet(manager, p);
                if (o == null) continue;

                try
                {
                    if (o is int i) return i;
                    if (o is long l) return (int)l;
                    if (o is double d) return (int)d;
                    if (int.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        return v;
                }
                catch { }
            }
            return 0;
        }

        private static bool ReadBool(PluginManager manager, params string[] paths)
        {
            foreach (var p in paths)
            {
                var o = TryGet(manager, p);
                if (o == null) continue;

                try
                {
                    if (o is bool b) return b;
                    if (o is int i) return i != 0;
                    if (bool.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), out var v))
                        return v;
                }
                catch { }
            }
            return false;
        }

        private static string ReadString(PluginManager manager, params string[] paths)
        {
            foreach (var p in paths)
            {
                var o = TryGet(manager, p);
                var s = o == null ? null : Convert.ToString(o, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
            }
            return string.Empty;
        }

        private static double ReadSeconds(PluginManager manager, params string[] paths)
        {
            foreach (var p in paths)
            {
                var o = TryGet(manager, p);
                if (o == null) continue;

                try
                {
                    if (o is TimeSpan ts) return Math.Max(0, ts.TotalSeconds);

                    var s = Convert.ToString(o, CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(s)) continue;

                    // TimeSpan-like string (e.g., "00:01:28.6910000")
                    if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var parsed))
                        return Math.Max(0, parsed.TotalSeconds);

                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        return Math.Max(0, d);
                }
                catch { }
            }
            return 0;
        }
    }
}
