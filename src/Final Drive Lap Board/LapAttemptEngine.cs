using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
//using System.Runtime.Serialization;
//using System.Runtime.Serialization.Json;

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

    public sealed class LapCommitResult
    {
        public DateTime Timestamp { get; set; }
        public string Driver { get; set; } = string.Empty;
        public string Car { get; set; } = string.Empty;
        public string Track { get; set; } = string.Empty;
        public string Conditions { get; set; } = string.Empty;

        public TimeSpan BestLap { get; set; }
        public bool WasCommitted { get; set; }
        public int TotalLapsInAttempt { get; set; }
        public int ValidatedLapCount { get; set; }
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
            public string Conditions = "Dry";
            public TimeSpan BestLap;
        }

        private readonly object _sync = new object();

        private string _driver = string.Empty;
        private string _car = string.Empty;
        private string _track = string.Empty;
        private string _conditions = "Dry";
        private int _lapsPerAttempt = 3;
        private bool _attemptActive;

        private readonly List<LapRecord> _attemptLaps = new List<LapRecord>();
        private readonly List<BoardEntry> _board = new List<BoardEntry>();

        // --- Metadata (UI can set these before StartAttempt) ---
        public string DriverName { get { lock (_sync) return _driver; } set { lock (_sync) _driver = value ?? string.Empty; } }
        public string CarName { get { lock (_sync) return _car; } set { lock (_sync) _car = value ?? string.Empty; } }
        public string TrackName { get { lock (_sync) return _track; } set { lock (_sync) _track = value ?? string.Empty; } }
        public string Conditions { get { lock (_sync) return _conditions; } set { lock (_sync) _conditions = string.IsNullOrWhiteSpace(value) ? "Dry" : value; } }

        public void ConfigureSession(string driver, string car, string track, int lapsPerSet, string conditions)
        {
            lock (_sync)
            {
                _driver = driver ?? string.Empty;
                _car = car ?? string.Empty;
                _track = track ?? string.Empty;
                _conditions = string.IsNullOrWhiteSpace(conditions) ? "Dry" : conditions;

                _lapsPerAttempt = lapsPerSet > 0 ? lapsPerSet : 3;
            }
        }

        public void StartAttempt()
        {
            lock (_sync)
            {
                _attemptLaps.Clear();
                _attemptActive = true;
            }
        }

        public void AbortAttempt()
        {
            lock (_sync)
            {
                _attemptLaps.Clear();
                _attemptActive = false;
            }
        }

        public void ResetBoard()
        {
            lock (_sync)
            {
                _board.Clear();
            }
        }

        public void OnLapCompleted(TimeSpan lapTime, bool gameLapValid)
        {
            if (lapTime.TotalSeconds <= 0.1) return;

            lock (_sync)
            {
                if (!_attemptActive) return;

                var record = new LapRecord
                {
                    AttemptIndex = _attemptLaps.Count,
                    LapTime = lapTime,
                    Validity = gameLapValid ? LapValidityTriState.Unknown : LapValidityTriState.Invalid
                };
                _attemptLaps.Add(record);
            }
        }

        public void SetLapValidity(int attemptIndex, LapValidityTriState validity)
        {
            lock (_sync)
            {
                if (attemptIndex < 0 || attemptIndex >= _attemptLaps.Count) return;
                _attemptLaps[attemptIndex].Validity = validity;
            }
        }

        public AttemptSnapshot GetSnapshot()
        {
            List<LapRecord> copy;
            int lapsPerSet;
            bool isActive;

            lock (_sync)
            {
                copy = _attemptLaps.ToList();
                lapsPerSet = _lapsPerAttempt > 0 ? _lapsPerAttempt : 3;
                isActive = _attemptActive;
            }

            var laps = copy.Select(l => new LapSnapshot
            {
                AttemptIndex = l.AttemptIndex,
                DisplayIndex = l.AttemptIndex + 1,
                LapTime = l.LapTime,
                Validity = l.Validity
            }).ToArray();

            return new AttemptSnapshot
            {
                LapsPerSet = lapsPerSet,
                IsActive = isActive,
                Laps = laps
            };
        }

        public LapCommitResult CommitAttempt()
        {
            List<LapRecord> laps;
            string driver;
            string car;
            string track;
            string conditions;

            lock (_sync)
            {
                laps = _attemptLaps.ToList();
                driver = _driver;
                car = _car;
                track = _track;
                conditions = _conditions;

                _attemptLaps.Clear();
                _attemptActive = false;
            }

            var validated = laps.Where(l => l.Validity == LapValidityTriState.Valid).ToList();
            bool committed = validated.Count > 0;

            var result = new LapCommitResult
            {
                Timestamp = DateTime.Now,
                Driver = driver ?? string.Empty,
                Car = car ?? string.Empty,
                Track = track ?? string.Empty,
                Conditions = string.IsNullOrWhiteSpace(conditions) ? "Dry" : conditions,
                WasCommitted = committed,
                TotalLapsInAttempt = laps.Count,
                ValidatedLapCount = validated.Count,
                BestLap = committed ? validated.Min(l => l.LapTime) : TimeSpan.Zero
            };

            if (committed)
            {
                lock (_sync)
                {
                    _board.Add(new BoardEntry
                    {
                        Timestamp = result.Timestamp,
                        Driver = result.Driver,
                        Car = result.Car,
                        Track = result.Track,
                        Conditions = result.Conditions,
                        BestLap = result.BestLap
                    });
                }
            }

            return result;
        }

        public void SaveAttemptCsv(string path)
        {
            List<LapRecord> laps;
            lock (_sync) laps = _attemptLaps.ToList();

            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                sw.WriteLine("Index,LapTimeSeconds,LapTimeFormatted,Validity");
                foreach (var l in laps)
                {
                    var formatted = FormatLapTimeTenths(l.LapTime);
                    var marker = ValidityMarker(l.Validity);
                    sw.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1:0.000},{2},{3}",
                        l.AttemptIndex + 1,
                        l.LapTime.TotalSeconds,
                        formatted,
                        marker));
                }
            }
        }

        public void SaveBoard(string path)
        {
            try
            {
                List<BoardEntry> board;
                lock (_sync) board = _board.ToList();

                // JSONL (one object per line) inside a .json file.
                // Why: we can parse it ourselves reliably without extra assemblies.
                using (var sw = new StreamWriter(path, false, Encoding.UTF8))
                {
                    foreach (var b in board)
                    {
                        sw.WriteLine(ToJsonLine(b));
                    }
                }
            }
            catch { /* swallow */ }
        }

        public void LoadBoard(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                var list = new List<BoardEntry>();
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var trimmed = (line ?? string.Empty).Trim();
                    if (trimmed.Length == 0) continue;

                    if (TryParseJsonLine(trimmed, out var entry))
                    {
                        list.Add(entry);
                    }
                }

                lock (_sync)
                {
                    _board.Clear();
                    _board.AddRange(list);
                }
            }
            catch { /* swallow */ }
        }

        public void ExportBoardCsv(string path)
        {
            List<BoardEntry> board;
            lock (_sync) board = _board.ToList();

            var ordered = board.OrderBy(b => b.BestLap).ToList();

            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                sw.WriteLine("Date,Track,Car,Driver,LapTimeFormatted,LapTimeSeconds,Conditions");
                foreach (var b in ordered)
                {
                    var formatted = FormatLapTimeTenths(b.BestLap);
                    sw.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5:0.000},{6}",
                        b.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        EscapeCsv(b.Track),
                        EscapeCsv(b.Car),
                        EscapeCsv(b.Driver),
                        formatted,
                        b.BestLap.TotalSeconds,
                        EscapeCsv(b.Conditions)));
                }
            }
        }

        public string BoardText
        {
            get
            {
                List<BoardEntry> board;
                lock (_sync) board = _board.ToList();

                if (board.Count == 0) return string.Empty;

                var ordered = board.OrderBy(b => b.BestLap).ToList();
                var lines = ordered.Select(b =>
                {
                    var date = b.Timestamp.ToString("yyyy MMM dd", CultureInfo.InvariantCulture).ToUpperInvariant();
                    return string.Format(CultureInfo.InvariantCulture,
                        "{0}  {1}  {2}  {3}  {4}  {5}",
                        date,
                        b.Track ?? string.Empty,
                        b.Car ?? string.Empty,
                        b.Driver ?? string.Empty,
                        FormatLapTimeTenths(b.BestLap),
                        b.Conditions ?? "Dry");
                });

                return string.Join(Environment.NewLine, lines);
            }
        }

        // --- helpers ---
        private static string ToJsonLine(BoardEntry b)
        {
            // Keep keys stable so our parser stays trivial
            return "{" +
                   "\"ts\":\"" + JsonEscape(b.Timestamp.ToString("o", CultureInfo.InvariantCulture)) + "\"," +
                   "\"driver\":\"" + JsonEscape(b.Driver ?? "") + "\"," +
                   "\"car\":\"" + JsonEscape(b.Car ?? "") + "\"," +
                   "\"track\":\"" + JsonEscape(b.Track ?? "") + "\"," +
                   "\"cond\":\"" + JsonEscape(b.Conditions ?? "Dry") + "\"," +
                   "\"best\":" + b.BestLap.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) +
                   "}";
        }

        private static bool TryParseJsonLine(string json, out BoardEntry entry)
        {
            entry = null;
            try
            {
                // Super small “schema-aware” parser: we only read what we wrote.
                string ts = JsonGetString(json, "ts");
                string driver = JsonGetString(json, "driver");
                string car = JsonGetString(json, "car");
                string track = JsonGetString(json, "track");
                string cond = JsonGetString(json, "cond");
                double best = JsonGetNumber(json, "best");

                DateTime timestamp;
                if (!DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp))
                    timestamp = DateTime.Now;

                entry = new BoardEntry
                {
                    Timestamp = timestamp,
                    Driver = driver ?? "",
                    Car = car ?? "",
                    Track = track ?? "",
                    Conditions = string.IsNullOrWhiteSpace(cond) ? "Dry" : cond,
                    BestLap = TimeSpan.FromSeconds(best < 0 ? 0 : best)
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string JsonEscape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string JsonUnescape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static string JsonGetString(string json, string key)
        {
            // looks for "key":"value"
            string needle = "\"" + key + "\":\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return "";
            i += needle.Length;

            var sb = new StringBuilder();
            bool esc = false;
            for (; i < json.Length; i++)
            {
                char c = json[i];
                if (esc)
                {
                    sb.Append('\\').Append(c); // keep escape sequence intact for JsonUnescape
                    esc = false;
                    continue;
                }
                if (c == '\\') { esc = true; continue; }
                if (c == '"') break;
                sb.Append(c);
            }
            return JsonUnescape(sb.ToString());
        }

        private static double JsonGetNumber(string json, string key)
        {
            // looks for "key":123.456
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return 0;
            i += needle.Length;

            int end = i;
            while (end < json.Length && ("-0123456789.".IndexOf(json[end]) >= 0)) end++;

            var s = json.Substring(i, end - i).Trim();
            double v;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return 0;
            return v;
        }

        public static string FormatLapTimeHundredths(TimeSpan t)
        {
            int totalMinutes = (int)t.TotalMinutes;
            int seconds = t.Seconds;
            int hundredths = t.Milliseconds / 10;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:00}", totalMinutes, seconds, hundredths);
        }

        public static string FormatLapTimeTenths(TimeSpan t)
        {
            int totalMinutes = (int)t.TotalMinutes;
            int seconds = t.Seconds;
            int tenths = t.Milliseconds / 100;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2}", totalMinutes, seconds, tenths);
        }

        private static string ValidityMarker(LapValidityTriState v)
        {
            switch (v)
            {
                case LapValidityTriState.Valid: return "V";
                case LapValidityTriState.Invalid: return "X";
                default: return "?";
            }
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
