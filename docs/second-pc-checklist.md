# Startup tab — checks for the Owner (G-002 M4)

Two short runs that need a person: the administrator prompt cannot be answered by automation,
and criterion 6 needs a second PC. Each step says what to expect; note anything different.
Any switch flipped here can be flipped back from the app, Settings or Task Manager. Nothing is deleted.

## A. On this PC (C:\bin\MemoryMap.exe)

1. Open Memory Map > Startup. Pick a machine-wide entry that is **off** and that you do not need,
   e.g. *Media Check Tool*. Its switch tooltip mentions administrator rights.
2. Flip it on. A Windows administrator prompt appears, naming MemoryMap.exe. Choose **No**.
   Expect: the switch springs back to off, and the notice says the prompt was declined and nothing changed.
3. Flip it on again and choose **Yes**. Expect: the switch stays on, and the notice says it will start at sign-in.
4. Open Task Manager > Startup apps and Settings > Apps > Startup. Expect: both show it **Enabled**.
5. Flip it back off in Memory Map (one more prompt, **Yes**). Expect: Task Manager and Settings show it Disabled
   again after a refresh.
6. Flip a Store app's switch (e.g. *Calendar*). Expect: Settings opens on its Startup page and the row springs back.
   No prompt appears.

## B. On a second PC

Copy `C:\bin\MemoryMap.exe` to it (USB stick or a shared folder). It is unsigned, so on first run SmartScreen
may warn ("Windows protected your PC"): choose *More info* > *Run anyway*. Best is a **standard (non-admin)
account**, Windows 10 or 11 x64, any display language.

1. The app starts and the Map tab fills within a few seconds.
2. Startup tab: the list matches Task Manager > Startup apps by name and by Enabled/Disabled.
   Note any entry that is missing, extra or in a different state.
3. Switch one of your own entries off and back on (no prompt). Expect: Task Manager shows each change after a refresh.
4. Repeat A.2–A.3 for one machine-wide entry, if the PC has one. On a standard account the prompt asks for
   an administrator's password instead of Yes/No.
5. Optional: in Settings > Apps > Startup, switch a machine-wide entry off, then check whether Memory Map shows it off
   too. This answers an open question (D018): whether Settings records that in this user's keys.
6. Open the History and Settings tabs, close the window (it goes to the tray), and choose Exit from the tray menu.

Report: Windows version and language, account type, and for each step "as expected" or what happened instead.
