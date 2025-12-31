Final Drive – Lap Board v1.0
============================

Top Gear–style lap board for SimHub.
Track your own hot lap attempts per car/track/conditions and export everything to CSV.

------------------------------------
1. CONTENTS
------------------------------------

This archive should contain:

- FinalDriveLapBoard.dll
- README.txt  (this file)

------------------------------------
2. INSTALLATION
------------------------------------

1. Close SimHub if it’s running.
2. Copy `FinalDriveLapBoard.dll` into your SimHub Plugins folder, for example:

   C:\Program Files (x86)\SimHub\Plugins\

3. Start SimHub.
4. Go to: Settings → Plugins
5. Enable **Final Drive Lap Board**.
6. A new **Final Drive Lap Board** tab will appear in the SimHub main window.

------------------------------------
3. BASIC USAGE
------------------------------------

Header fields:
- Driver: free text.
- Car / Track: editable dropdowns (you can type new values).
- Laps/set: how many valid laps should make up a “set”.
- Conditions: Dry / Wet / Very Wet / Sleet/Snow / Fog.

If Car / Track are left blank, the plugin will try to auto-fill them from telemetry
(CarModel / TrackName) when you start attempts.

Buttons in "Current attempt":
- START ATTEMPTS
  - Applies the header values.
  - Clears the current attempt list.
  - Starts recording laps as you drive.
- ABORT ATTEMPTS
  - Stops recording new laps but keeps the current list visible/exportable.
- COMMIT VALIDATED
  - Uses only laps you’ve marked **Valid**.
  - Picks up to “Laps/set” of the best valid laps.
  - Logs the best of those to the Lap Board.
- SAVE ATTEMPT TO CSV
  - Exports ALL laps in the attempt (valid / invalid / unknown) to CSV.

Laps list:
- Each row shows: index, time, and marker: [?] / [V] / [X].
- Checkbox is tri-state:
  - Checked      = Valid [V]
  - Unchecked    = Invalid [X]
  - Filled / null= Unknown [?]

Lap Board:
- One entry per committed attempt.
- Shows: date, track, car, driver, lap time (M:SS.t), and conditions.
- Sorted by fastest lap first.
- Buttons:
  - EXPORT BOARD TO CSV
  - RESET BOARD

------------------------------------
4. FILES / OUTPUT
------------------------------------

All plugin data is stored under:

Documents\SimHub\Final Drive Lap Board\

You will see:
- cars_tracks.ini
  - Simple catalog backing the Car / Track dropdowns.
- LapAttempts_YYYYMMDD_HHMM.csv
  - Export of a single attempt’s laps.
- LapBoard_YYYYMMDD_HHMM.csv
  - Export of the full lap board.

------------------------------------
5. LICENSE / USAGE TERMS
------------------------------------

Final Drive – Lap Board is free for personal and community use.

You may use, modify, and share this plugin and its source for non-commercial purposes.

Commercial use — including selling, paid bundles, paid add-ons, or monetized
distributions / repackaging — is NOT permitted without explicit permission from
the author.

Licensed under:
  Creative Commons Attribution–NonCommercial 4.0 (CC BY-NC 4.0)