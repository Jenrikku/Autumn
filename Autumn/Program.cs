using Autumn.GUI;
using Autumn.IO;
using Autumn.Storage;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

#if DEBUG
RomFSHandler.RomFSPath = ""; // Please, input your RomFS manually here.

StageHandler.TryImportStage("FirstStage", 1, out Stage stage);
ProjectHandler.ActiveProject.Stages.Add(stage);
#endif

WindowManager.Add(new MainWindowContext());
WindowManager.Run();
