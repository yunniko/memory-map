# D031 · Close-to-tray asks the desktop for a tray host at each close
Date: 2026-10-12 · Goal: G-003 M2 · Status: active (superseded by: —)
Context: Many Linux desktops (plain GNOME, WSLg) show no tray icons; hiding the window there would leave a running app with no way back to it.
Decision: Closing hides the window only if the setting is on and the desktop has a StatusNotifier host at that moment; otherwise it quits. `TrayHost` owns the answer: fixed on Windows, asked over D-Bus off the UI thread on Linux, the window kept until it answers.
Force: requirement — G-003 criterion 3.
Rejected: one check at startup (a tray extension switched off later would strand the window); asking on the UI thread (a stuck bus froze the close for up to 5 s); clearing the saved setting (lost on a desktop that later gains a tray).
Consequence: The saved choice stays as set; the Settings tab greys it out with a note while no host is shown. A desktop already known to have no host closes at once; no answer means quit.
Evidence: src/MemoryMap.App/TrayHost.cs; src/MemoryMap.App/MainWindow.axaml.cs; tests/MemoryMap.App.Tests/HistoryAndTrayTests.cs
