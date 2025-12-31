
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Final_Drive_Lap_Board
{
    /// <summary>
    /// Simple catalog for car and track dropdowns.
    /// Backed by a small INI-style file.
    /// </summary>
    public sealed class CarTrackCatalog
    {
        public string FilePath { get; }

        public List<string> Cars { get; } = new List<string>();
        public List<string> Tracks { get; } = new List<string>();

        public CarTrackCatalog(string filePath)
        {
            FilePath = filePath ?? string.Empty;
        }

        public void LoadOrCreateDefault()
        {
            EnsureDirectory();

            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, DefaultTemplate(), Encoding.UTF8);
            }

            Reload();
        }

        public void Reload()
        {
            Cars.Clear();
            Tracks.Clear();

            if (!File.Exists(FilePath)) return;

            string section = string.Empty;

            foreach (var raw in File.ReadAllLines(FilePath))
            {
                var line = (raw ?? string.Empty).Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith(";")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                if (section.Equals("Cars", StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(Cars, line);
                }
                else if (section.Equals("Tracks", StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(Tracks, line);
                }
            }

            Cars.Sort(StringComparer.OrdinalIgnoreCase);
            Tracks.Sort(StringComparer.OrdinalIgnoreCase);
        }

        public void RememberCar(string name) => AddUnique(Cars, name);
        public void RememberTrack(string name) => AddUnique(Tracks, name);

        public void Save()
        {
            EnsureDirectory();

            using (var sw = new StreamWriter(FilePath, false, Encoding.UTF8))
            {
                sw.WriteLine("# Final Drive Lap Board car/track catalog");
                sw.WriteLine();

                sw.WriteLine("[Cars]");
                foreach (var c in Cars.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    sw.WriteLine(c);

                sw.WriteLine();
                sw.WriteLine("[Tracks]");
                foreach (var t in Tracks.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    sw.WriteLine(t);
            }
        }

        private void EnsureDirectory()
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;
            var dir = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrWhiteSpace(dir)) return;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static void AddUnique(List<string> list, string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
                list.Add(value);
        }

        private static string DefaultTemplate()
        {
            return
@"# Final Drive Lap Board car/track catalog
[Cars]
Suzuki Liana
Kia Cee'd

[Tracks]
Top Gear Test Track
";
        }
    }
}
