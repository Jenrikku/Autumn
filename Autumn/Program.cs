using Autumn.Commands;
using Autumn.GUI;
using Autumn.IO;
using System.Text;

// Required to support Shift-JIS encoding.
// See System.Text.Encoding.CodePages package.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Sets the working directory to the one where the program is stored.
// This is required to properly find the resources.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

SettingsHandler.LoadSettings();

RomFSHandler.LoadFromSettings();
RecentHandler.LoadFromSettings();

CommandHandler.Initialize();

// Checks whether the welcome window has to be shown.
if (!SettingsHandler.GetValue<bool>("SkipWelcomeWindow"))
    WindowManager.Add(new WelcomeWindowContext());
else
    WindowManager.Add(new MainWindowContext());

WindowManager.Run();

// Everything past this point is executed when all windows are closed.

SettingsHandler.SaveSettings();
