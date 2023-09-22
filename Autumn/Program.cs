using Autumn.GUI;
using Autumn.IO;
using Autumn.Storage;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

SettingsHandler.LoadSettings();

RomFSHandler.LoadFromSettings();

if (!SettingsHandler.GetValue<bool>("SkipWelcomeWindow"))
    WindowManager.Add(new WelcomeWindowContext());
else
    WindowManager.Add(new MainWindowContext());

WindowManager.Run();

SettingsHandler.SaveSettings();
