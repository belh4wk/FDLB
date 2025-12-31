
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Final_Drive_Lap_Board
{
    public enum LapValidityTriState
    {
        Unknown,
        Valid,
        Invalid
    }

    public sealed class LapSnapshot
    {
        public int AttemptIndex { get; set; }   // 0-based internal index
        public int DisplayIndex { get; set; }   // 1-based for UI
        public TimeSpan LapTime { get; set; }
        public LapValidityTriState Validity { get; set; }
    }

    public sealed class AttemptSnapshot
    {
        public int LapsPerSet { get; set; }
        public bool IsActive { get; set; }
        public LapSnapshot[] Laps { get; set; } = Array.Empty<LapSnapshot>();
    }

    public sealed class LapAttemptEngine
    {
        private sealed class LapRecord
        {
            public int AttemptIndex;
            public TimeSpan LapTime;
            public LapValidityTriState Validity;
        }

        private sealed class BoardEntry
        {
            public DateTime Timestamp;
            public string Driver = string.Empty;
            public string Car = string.Empty;
            public string Track = string.Empty;
            public string Conditions = string.Empty;
            public TimeSpan BestLap;
        }

        private string _driver = string.Empty;
        private string _car = string.Empty;
        private string _track = string.Empty;
        private string _conditions = "Dry";
        private int _lapsPerAttempt = 3;
        private bool _attemptActive;

        private readonly List<LapRecord> _attemptLaps = new List<LapRecord>();
        private readonly List<BoardEntry> _board = new List<BoardEntry>();

        public void ConfigureSession(string driver, string car, string track, int lapsPerSet, string conditions)
        {
            _driver = driver ?? string.Empty;
            _car = car ?? string.Empty;
            _track = track ?? string.Empty;
            _conditions = string.IsNullOrWhiteSpace(conditions) ? "Dry" : conditions;

            _lapsPerAttempt = lapsPerSet > 0 ? lapsPerSet : 3;
        }

        public void StartAttempt()
        {
            _attemptLaps.Clear();
            _attemptActive = true;
        }

        public void AbortAttempt()
        {
            // Stops recording new laps but keeps the list so you can inspect/export.
            _attemptActive = false;
        }

        public void ResetBoard()
        {
            _board.Clear();
        }

        public void OnLapCompleted(TimeSpan lapTime, bool gameLapValid)
        {
            if (!_attemptActive) return;
            if (lapTime.TotalSeconds <= 0.1) return;

            var record = new LapRecord
            {
                AttemptIndex = _attemptLaps.Count,
                LapTime = lapTime,
                Validity = gameLapValid ? LapValidityTriState.Unknown : LapValidityTriState.Invalid
            };

            _attemptLaps.Add(record);
        }

        public void SetLapValidity(int attemptIndex, LapValidityTriState validity)
        {
            if (attemptIndex < 0 || attemptIndex >= _attemptLaps.Count) return;
            _attemptLaps[attemptIndex].Validity = validity;
        }

        public AttemptSnapshot GetSnapshot()
        {
            var laps = _attemptLaps
                .Select(l => new LapSnapshot
                {
                    AttemptIndex = l.AttemptIndex,
                    DisplayIndex = l.AttemptIndex + 1,
                    LapTime = l.LapTime,
                    Validity = l.Validity
                })
                .ToArray();

            return new AttemptSnapshot
            {
                LapsPerSet = _lapsPerAttempt > 0 ? _lapsPerAttempt : 3,
                IsActive = _attemptActive,
                Laps = laps
            };
        }

        public void CommitAttempt()
        {
            // Only consider laps explicitly marked Valid.
            var validLaps = _attemptLaps
                .Where(l => l.Validity == LapValidityTriState.Valid)
                .ToList();

            if (validLaps.Count == 0)
            {
                // Nothing valid to commit.
                return;
            }

            int targetCount = _lapsPerAttempt > 0 ? _lapsPerAttempt : validLaps.Count;
            targetCount = Math.Min(targetCount, validLaps.Count);

            var bestSubset = validLaps
                .OrderBy(l => l.LapTime)
                .Take(targetCount)
                .ToList();

            var bestLap = bestSubset.Min(l => l.LapTime);

            _board.Add(new BoardEntry
            {
                Timestamp = DateTime.Now,
                Driver = _driver,
                Car = _car,
                Track = _track,
                Conditions = _conditions,
                BestLap = bestLap
            });

            _attemptActive = false;
            // NOTE: We do NOT clear _attemptLaps here; only StartAttempt clears.
        }

        public void SaveAttemptCsv(string path)
        {
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                sw.WriteLine("Index,LapTimeSeconds,LapTimeFormatted,Validity");
                foreach (var l in _attemptLaps)
                {
                    string marker = ValidityMarker(l.Validity);
                    string formatted = FormatLapTimeTenths(l.LapTime);
                    sw.WriteLine($"{l.AttemptIndex + 1},{l.LapTime.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)},{formatted},{marker}");
                }
            }
        }

        public void ExportBoardCsv(string path)
        {
            var ordered = _board.OrderBy(b => b.BestLap).ToList();

            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                sw.WriteLine("Date,Track,Car,Driver,LapTimeFormatted,LapTimeSeconds,Conditions");
                foreach (var b in ordered)
                {
                    string formatted = FormatLapTimeTenths(b.BestLap);
                    sw.WriteLine($"{b.Timestamp:yyyy-MM-dd},{Escape(b.Track)},{Escape(b.Car)},{Escape(b.Driver)},{formatted},{b.BestLap.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)},{Escape(b.Conditions)}");
                }
            }
        }

        public string BoardText
        {
            get
            {
                if (_board.Count == 0) return string.Empty;

                var ordered = _board.OrderBy(b => b.BestLap).ToList();
                var lines = ordered.Select(b =>
                    $"{b.Timestamp:yyyy-MM-dd} | {b.Track} | {b.Car} | {b.Driver} | {FormatLapTimeTenths(b.BestLap)} | {b.Conditions}");

                return string.Join(Environment.NewLine, lines);
            }
        }

        // --- Formatting helpers ---

        public static string FormatLapTimeTenths(TimeSpan t)
        {
            int totalMinutes = (int)t.TotalMinutes;
            int seconds = t.Seconds;
            int tenths = t.Milliseconds / 100;
            return $"{totalMinutes}:{seconds:00}.{tenths}";
        }

        private static string ValidityMarker(LapValidityTriState v)
        {
            switch (v)
            {
                case LapValidityTriState.Valid: return "[V]";
                case LapValidityTriState.Invalid: return "[X]";
                default: return "[?]";
            }
        }

        private static string Escape(string v)
        {
            if (v == null) return string.Empty;
            if (v.Contains(",") || v.Contains("""))
                return """ + v.Replace(""", """") + """;
            return v;
        }
    }
}
