using AutumnSceneGL.GUI;
using AutumnSceneGL.IO;
using AutumnSceneGL.Storage;
using ImGuiNET;
using System.Diagnostics;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

new MainWindow().AddToWindowManager();
WindowManager.Run();