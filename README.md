# Final Drive – Lap Board

Top Gear–style lap record board for SimHub.  
Track your own attempts per car/track/conditions and export everything to CSV.

---

## Usage & License

Final Drive – Lap Board is free for personal and community use.

You may use, modify, and share this project for non-commercial purposes.  
Commercial use — including selling, paid bundles, paid add-ons, or monetized distributions / repackaging — is **not** permitted without explicit permission.

Licensed under **Creative Commons Attribution–NonCommercial 4.0 (CC BY-NC 4.0)**.  
See the `LICENSE` file for details.

---

## Building from source (SimHub plugin)

The `src/FinalDriveLapBoard/` folder contains the full C# source for the plugin.

To build it yourself:

1. **Install SimHub**  
   Make sure SimHub is installed and working. You’ll need its assemblies as references:
   - `SimHub.Plugins.dll`
   - `GameReaderCommon.dll`  
   (Both are in your SimHub install folder.)

2. **Create a C# Class Library project**
   - Target **.NET Framework 4.7.2 or later** (same as your SimHub version).
   - Add the files from `src/FinalDriveLapBoard/` into the project:
     - `FDLBPlugin.cs`
     - `LapAttemptEngine.cs`
     - `TelemetryReader.cs`
     - `CarTrackCatalog.cs`
     - `FDLBSettingsControl.xaml`
     - `FDLBSettingsControl.xaml.cs`

3. **Add SimHub references**
   - In your project, add references to:
     - `SimHub.Plugins.dll`
     - `GameReaderCommon.dll`
   - Make sure `FDLBSettingsControl.xaml` has **Build Action = Page**.

4. **Build output**
   - Set the project output path to your SimHub `Plugins` folder  
     (e.g. `C:\Program Files (x86)\SimHub\Plugins\`), or copy the built DLL there manually after each build.

5. **Enable the plugin**
   - Restart SimHub.
   - Go to **Settings → Plugins** and enable **Final Drive Lap Board**.
   - A new **Final Drive Lap Board** tab will appear in the SimHub UI.

You can now use the plugin, inspect/modify the source, and share non‑commercial builds as long as you respect the license above.
