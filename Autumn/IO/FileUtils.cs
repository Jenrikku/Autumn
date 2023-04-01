namespace AutumnSceneGL.IO {
    internal class FileUtils {
        public static List<(string name, byte? scenario)> ListStages(string path) {
            List<(string name, byte? scenario)> stages = new();

            if(!Directory.Exists(path))
                return stages;

            foreach(string file in Directory.EnumerateFiles(path)) {
                (string name, byte? scenario) result;

                string name = Path.GetFileName(file);

                int index = name.LastIndexOf("Stage");

                if(index == 0 || !name.EndsWith(".szs"))
                    continue;

                int scenarioIndex = name.Length - 5;

                bool hasScenario = byte.TryParse(name.AsSpan(scenarioIndex, 1), out byte scenario);

                name = name[..index];

                if(hasScenario)
                    result = (name, scenario);
                else
                    result = (name, null);

                if(!stages.Contains(result))
                    stages.Add(result);
            }

            return stages;
        }
    }
}
