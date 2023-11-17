using Autumn.GUI;
using Autumn.IO;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

SettingsHandler.LoadSettings();

RomFSHandler.LoadFromSettings();
RecentHandler.LoadFromSettings();

if (!SettingsHandler.GetValue<bool>("SkipWelcomeWindow"))
    WindowManager.Add(new WelcomeWindowContext());
else
    WindowManager.Add(new MainWindowContext());

WindowManager.Run();

SettingsHandler.SaveSettings();
