
using System;
using System.Globalization;
using GameReaderCommon;
using SimHub.Plugins;

namespace Final_Drive_Lap_Board
{
    /// <summary>
    /// Minimal telemetry adapter: detects completed laps and exposes current lap time + last car/track.
    /// </summary>
    public sealed class TelemetryReader
    {
        private int _lastCompletedLaps;

        public double CurrentLapTimeSeconds { get; private set; }

        public string LastCarName { get; private set; } = string.Empty;
        public string LastTrackName { get; private set; } = string.Empty;

        public void ResetLapCounter()
        {
            _lastCompletedLaps = 0;
        }

        public void Tick(PluginManager manager, ref GameData data, LapAttemptEngine engine)
        {
            if (manager == null || engine == null) return;

            int completedLaps = 0;
            double lastLapTime = 0;
            bool lapValid = true;
            double currentLapTime = 0;
            string carName = null;
            string trackName = null;

            try
            {
                var o = manager.GetPropertyValue("DataCorePlugin.GameData.CompletedLaps");
                if (o != null) completedLaps = Convert.ToInt32(o, CultureInfo.InvariantCulture);
            }
            catch { }

            try
            {
                var o = manager.GetPropertyValue("DataCorePlugin.GameData.LastLapTime");
                if (o != null) lastLapTime = Convert.ToDouble(o, CultureInfo.InvariantCulture);
            }
            catch { }

            try
            {
                var o = manager.GetPropertyValue("DataCorePlugin.GameData.LapValid");
                if (o != null) lapValid = Convert.ToBoolean(o, CultureInfo.InvariantCulture);
            }
            catch { }

            try
            {
                var o = manager.GetPropertyValue("DataCorePlugin.GameData.CurrentLapTime");
                if (o != null) currentLapTime = Convert.ToDouble(o, CultureInfo.InvariantCulture);
            }
            catch { }

            try
            {
                var o = manager.GetPropertyValue("DataCorePlugin.GameData.CarModel");
                if (o != null) carName = o.ToString();
            }
            catch { }

            try
            {
                var o = manager.GetPropertyValue("DataCorePlugin.GameData.TrackName");
                if (o != null) trackName = o.ToString();
            }
            catch { }

            CurrentLapTimeSeconds = currentLapTime;

            if (!string.IsNullOrWhiteSpace(carName)) LastCarName = carName;
            if (!string.IsNullOrWhiteSpace(trackName)) LastTrackName = trackName;

            if (completedLaps > _lastCompletedLaps && lastLapTime > 0.1)
            {
                engine.OnLapCompleted(TimeSpan.FromSeconds(lastLapTime), lapValid);
                _lastCompletedLaps = completedLaps;
            }
        }
    }
}
