using AutumnSceneGL.IO;
using System.Diagnostics;

namespace AutumnSceneGL.Storage {
    internal static class Project {
        private static ProjectInstance _instance = new();
        private static ProjectContents _contents = new("Unnamed project");

        /// <summary>
        /// An event called when the current project's name has changed.
        /// </summary>
        //public static event Action<string>? ProjectNameChanged;

        public static bool IsLoaded => _instance is not null;

        public static string? SavePath {
            get => _instance.SavePath;
            set => _instance.SavePath = value;
        }

        public static string Name {
            get => _contents.Name;
            set => _contents.Name = value;
        }

        public static List<Stage> Stages => _instance.Stages;
        public static Dictionary<string, object> Settings => _contents.Settings;

        public static bool ContainsFile(string relPath) =>
            _instance is not null && File.Exists(Path.Join(_instance.SavePath, relPath));

        /// <summary>
        /// Chnages the current project's name and invokes <see cref="ProjectNameChanged"/>
        /// </summary>
        /// <param name="name"></param>
        //public static void ChangeName(string name) {

        //}

        public static bool TryGetFile(string relPath, out byte[]? file) {
            file = null;

            if(!ContainsFile(relPath))
                return false;

            try {
                file = File.ReadAllBytes(Path.Join(_instance?.SavePath, relPath));
            } catch {
                return false;
            }

            return true;
        }

        /// <returns>Whether the given path contains a valid project.</returns>
        public static bool Validate(string path) =>
            File.Exists(Path.Join(path, ".autumnproj"));

        public static void Load(string path) { // path => Directory containing .autumnproj file.
            if(!Unload() || !Validate(path))
                return;

            _instance.SavePath = path;

            // Add stages (This does not read the contents of them):
            foreach((string stage, byte? scenario) in FileUtils.ListStages(Path.Join(path, "StageData")))
                _instance.Stages.Add(new(stage, scenario));

            // Load project contents:
            _contents = YAMLWrapper.Desearialize<ProjectContents>(Path.Join(path, ".autumnproj"));
        }

        /// <returns>Whether the project was unload successfully.</returns>
        public static bool Unload() {
            // Unload stages. (Prompt the user to save first)
            foreach(Stage? stage in _instance.Stages) {
                if(stage is null)
                    continue;


            }

            _instance = new();
            _contents = new();

            return true;
        }

        public static void SaveSettings(string path = "") {
            if(string.IsNullOrEmpty(path)) {
                if(string.IsNullOrEmpty(_instance?.SavePath))
                    return;

                path = _instance?.SavePath ?? string.Empty;
            }

            path = Path.Join(path, ".autumnproj");

            YAMLWrapper.Serialize(path, _contents);
        }


        private class ProjectInstance {
            public string? SavePath;

            public readonly List<Stage> Stages = new();
            public readonly List<Stage> LoadedStages = new();
        }

        private struct ProjectContents {
            public string Name;
            public Dictionary<string, object> Settings = new();

            public ProjectContents(string name) => Name = name;
        }
    }
}
