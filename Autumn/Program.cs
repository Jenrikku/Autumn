using System.Text;
using Autumn.Context;
using Autumn.GUI;
using Autumn.GUI.Windows;

// Required to support Shift-JIS encoding.
// See System.Text.Encoding.CodePages package.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Get Autumn's config path.
string configPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
configPath = Path.Join(configPath, "autumn");

// Create the ContextHandler.
// It contains the active project and settings.
ContextHandler contextHandler = new(configPath);

// Load project from argument if available.
// It will do nothing if the argument is not a proper project.
if (args.Length > 0)
    contextHandler.OpenProject(args[0]);
else if (contextHandler.SystemSettings.OpenLastProject)
    // Load last opened project. Does nothing if empty.
    contextHandler.OpenProject(contextHandler.SystemSettings.LastOpenedProject);

// Sets the working directory to the one where the program is stored.
// This is required to properly find the resources.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// Set up the window manager:

WindowManager windowManager = new();

windowManager.Add(new MainWindowContext(contextHandler, windowManager));

windowManager.Run(contextHandler.ActionHandler);

// Everything past this point is executed when all windows are closed:

contextHandler.SetCurrentProjectAsLastOpened();
contextHandler.SaveSettings();
