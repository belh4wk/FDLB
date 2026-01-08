using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Final_Drive_Lap_Board
{
    /// <summary>
    /// Minimal MCP4H-style emitter writing one lap_result event per line to JSONL.
    /// </summary>
    public sealed class Mcp4HLapEmitter
    {
        private readonly string _filePath;
        private readonly object _sync = new object();

        public Mcp4HLapEmitter(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath must not be null or empty.", nameof(filePath));

            _filePath = filePath;
        }

        public void AppendLapResult(LapCommitResult result)
        {
            if (result == null) return;

            // MCP4H v0.1.1 envelope, using the 0.1.1 schema with extensions.
            // version must be "mcp4h/0.1" per schema.
            string version = "mcp4h/0.1";

            // Basic ID: you can swap this to something deterministic if you want.
            string id = Guid.NewGuid().ToString();

            string timestamp = result.Timestamp
                .ToUniversalTime()
                .ToString("o", CultureInfo.InvariantCulture);

            // Lap time values
            double seconds = result.BestLap.TotalSeconds;
            // Round to nearest hundredth
            int hundredths = (int)Math.Round(seconds * 100.0, MidpointRounding.AwayFromZero);

            string formattedTenths = LapAttemptEngine.FormatLapTimeTenths(result.BestLap);

            var sb = new StringBuilder();

            sb.Append('{');

            // version
            sb.Append("\"version\":\"").Append(version).Append("\",");

            // id
            sb.Append("\"id\":").Append(JsonString(id)).Append(',');

            // timestamp
            sb.Append("\"timestamp\":").Append(JsonString(timestamp)).Append(',');

            // origin
            sb.Append("\"origin\":{");
            sb.Append("\"platform\":").Append(JsonString("fdlb")).Append(',');
            sb.Append("\"relation\":").Append(JsonString("telemetry"));
            sb.Append("},");

            // actor
            sb.Append("\"actor\":{");
            sb.Append("\"role\":").Append(JsonString("system")).Append(',');
            sb.Append("\"handle\":").Append(JsonString("FinalDriveLapBoard"));
            sb.Append("},");

            // human-readable text (optional, but nice)
            sb.Append("\"text\":");
            sb.Append(JsonString(
                $"Lap result committed: driver {result.Driver}, car {result.Car}, track {result.Track}, time {formattedTenths}, {result.Conditions}."));
            sb.Append(',');

            // metadata (required fields: heat, valence)
            sb.Append("\"metadata\":{");
            sb.Append("\"heat\":0,");
            sb.Append("\"valence\":\"neutral\"");
            sb.Append("},");

            // extensions.fdlb_lap_result
            sb.Append("\"extensions\":{");
            sb.Append("\"fdlb_lap_result\":{");

            sb.Append("\"driver\":").Append(JsonString(result.Driver)).Append(',');
            sb.Append("\"car\":").Append(JsonString(result.Car)).Append(',');
            sb.Append("\"track\":").Append(JsonString(result.Track)).Append(',');
            sb.Append("\"conditions\":").Append(JsonString(result.Conditions)).Append(',');
            sb.Append("\"lap_time_hundredths\":")
              .Append(hundredths.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"lap_time_seconds\":")
              .Append(seconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"rules\":[");
            sb.Append(JsonString("best_of_n_valid_laps")).Append(',');
            sb.Append(JsonString("n_equals_laps_per_set")).Append(',');
            sb.Append(JsonString("laps_per_set_configurable"));
            sb.Append("]"); // end rules array

            sb.Append('}');  // end fdlb_lap_result
            sb.Append('}');  // end extensions

            sb.Append('}');  // end envelope

            string line = sb.ToString();

            lock (_sync)
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }


        private static string JsonString(string value)
        {
            if (value == null) return "null";

            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}