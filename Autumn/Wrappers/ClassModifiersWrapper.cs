using System.Numerics;
using Autumn.Enums;
using Autumn.Utils;
using SharpYaml.Serialization;

namespace Autumn.Wrappers;

internal static class ClassModifiersWrapper
{
    public struct ModifierBase
    {
        public ModifierEntry? Default { get; set; }
        public Dictionary<string, ModifierEntry>? Variants { get; set; }
    }
    public struct ModifierEntry
    {
        public string? ModelReplace { get; set; } // Implemented
        public Dictionary<string, ExtraModel?>? ExtraModels { get; set; } // Implemented
        public Vector3? Translation { get; set; } // Implemented
        public Vector3? Scale { get; set; } // Implemented
        public Vector3? Rotation { get; set; } // Implemented
        public List<string>? HiddenMeshes { get; set; } // Implemented
        public Dictionary<string, byte>? MeshesPriority { get; set; }
        public Dictionary<string, ArgChange>? Args { get; set; } // ArgID, Arg
        [YamlIgnore]
        public Dictionary<string, BaseArg>? ArgsRem { get; set; } // ArgID, Arg
    }
    public struct ExtraModel
    {
        public Vector3? Translation { get; set; }
        public Vector3? Scale { get; set; }
        public Vector3? Rotation { get; set; }
        public Transform GetTransform()
        {
            Transform t = new();
            if (Translation != null)
                t.Translate = Translation.Value;
            if (Scale != null)
                t.Scale = Scale.Value;
            if (Rotation != null)
                t.Rotate = Rotation.Value;
            return t;
        }
    }
    /// <summary>
    /// Temporary replacement for args
    /// </summary>
    [Obsolete]
    public struct BaseArgChange
    {
        public ArgType ArgType { get; set; }

        // Tower
        public string? RepeatModel { get; set; } // If no RepeatModel -> use basemodel
        public bool? CountTop { get; set; }
        public Vector3? Offset { get; set; }

        //Animation
        public string? AnimName { get; set; } // animation to use
        public H3DAnimation? AnimType { get; set; } // type of the animation
    }

    public class ArgChange
    {
        public ArgType ArgType { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }
    public interface BaseArg
    {

    }
    public class SimpleArg : BaseArg
    {
        public byte BasicTestValue = 0; 
    }
    public class TowerArg : BaseArg
    {
        public string? RepeatModel { get; set; } // If no RepeatModel -> use basemodel
        public bool? CountTop { get; set; } // whether the top model disappears at 0 or not
        public Vector3 Offset { get; set; }
        public TowerArg(Dictionary<string, object> props)
        {
            object? v;
            if (props.TryGetValue("RepeatModel", out v))
                RepeatModel = (string)v;
            if (props.TryGetValue("CountTop", out v))
                CountTop = (bool)v;
            if (props.TryGetValue("Offset", out v))
                Offset = FromDict((Dictionary<object,object>)v);
        }
    }
    public class ExtraArgModels : BaseArg
    {
        public Dictionary<string, ExtraModel> Models { get; set; } // If no RepeatModel -> use basemodel
        public ExtraArgModels(Dictionary<string, object> props)
        {
            object? v;
            if (props.TryGetValue("Models", out v))
                if (v is Dictionary<object, object>)
                {
                    Models = new();
                    var dc = (Dictionary<object, object>)v;
                    foreach (string s in dc.Keys)
                    {
                        if (dc[s] is not Dictionary<object, object>) 
                        {
                            Models.Add(s, new());
                            continue;
                        }
                        var EM = new ExtraModel();
                        if (((Dictionary<object, object>)dc[s]).TryGetValue("Translation", out v))
                            EM.Translation = FromDict((Dictionary<object, object>)v);
                        if (((Dictionary<object, object>)dc[s]).TryGetValue("Scale", out v))
                            EM.Scale = FromDict((Dictionary<object, object>)v);
                        if (((Dictionary<object, object>)dc[s]).TryGetValue("Rotation", out v))
                            EM.Rotation = FromDict((Dictionary<object, object>)v);
                        Models.Add(s, EM);
                    }
                }
        }
    }
    /// <summary>
    /// Plays the specific frame of an animation in the model file 
    /// Can be used for material and skeleton animations
    /// Uses value of the Arg as the frame
    /// </summary>
    public class AnimChangeArg : BaseArg
    {
        public string AnimName { get; set; } // animation to use
        public H3DAnimation AnimType { get; set; } // type of the animation
        // public int Frame { get; set; } // animation frame to use
        public AnimChangeArg(Dictionary<string, object> props)
        {
            object? v;
            if (props.TryGetValue("AnimName", out v))
                AnimName = (string)v;
            if (props.TryGetValue("AnimType", out v))
                AnimType = Enum.Parse<H3DAnimation>((string)v);
        }
    }

    /// <summary>
    /// Actor that has models (MiddleModel, PointModel) that rotate around it, one arg can set the amount of said RotateModel, another arg sets the number of lines of that
    /// Value of the arg -> number of balls
    /// </summary>
    public class RotateCoreCount : BaseArg
    {
    }
    /// <summary>
    /// Number of lines for this actor, they will be drawn as equal divisions of a circle (3 lines -> 120º between each)
    /// Value of the arg -> line count
    /// </summary>
    public class RotateCoreBars : BaseArg
    {
        /// <summary>
        /// if true,we check for the first arg that has RotateCoreCount when setting the bars,
        /// otherwise we use the actor's name to guess the number of balls (8M -> 8 balls per bar)
        /// </summary>
        public bool UseArgCount { get; set; } 
        public string PointModel { get; set; } // last repeated object
        public string MiddleModel { get; set; } // object to repeat in line
        public RotateCoreBars(Dictionary<string, object> props)
        {
            object? v;
            if (props.TryGetValue("UseArgCount", out v))
                UseArgCount = (bool)v;
            if (props.TryGetValue("MiddleModel", out v))
                MiddleModel = (string)v;
            if (props.TryGetValue("PointModel", out v))
                PointModel = (string)v;
        }
        
    }
    /// <summary>
    /// Actor that is separate from the origin by a given length
    /// Arg value -> Length
    /// </summary>
    public class SwingingCore : BaseArg
    {
        // public Vector3 HeadOffset { get; set; }
        public string HeadModel { get; set; }
        public string ChainModel { get; set; }
        public bool? TwoChains { get; set; }
        public Vector3? ChainDistance { get; set; }
        public SwingingCore(Dictionary<string, object> props)
        {
            object? v;
            if (props.TryGetValue("HeadModel", out v))
                HeadModel = (string)v;
            if (props.TryGetValue("ChainModel", out v))
                ChainModel = (string)v;
            if (props.TryGetValue("TwoChains", out v))
                TwoChains = (bool)v;
        }
    }
    public class ShadowType : BaseArg
    {

    }
    public class ScaleAxis : BaseArg
    {
        // public Vector3 HeadOffset { get; set; }
        public string Axis { get; set; }
        public ScaleAxis(Dictionary<string, object> props)
        {
            object? v;
            if (props.TryGetValue("Axis", out v))
                Axis = (string)v;
        }
    }


    /// <summary>
    /// Converts the X Y Z dictionary to a Vector3, assumes the dictionary is formatted properly
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static Vector3 FromDict(Dictionary<object, object> o) => new Vector3(Convert.ToSingle(o["X"]), Convert.ToSingle(o["Y"]), Convert.ToSingle(o["Z"])); 

    private static SortedDictionary<string, ModifierBase>? s_ModifierEntries = null; // ClassName / Entry 
    public static bool ReloadEntries = false;
    public static SortedDictionary<string, ModifierBase> ModifierEntries
    {
        get
        {
            if (s_ModifierEntries is null || ReloadEntries)
            {
                s_ModifierEntries = new();
                string path = Path.Join("Resources", "Modifiers");
                // ModifierBase bbb = new() { Variants = new() };
                // bbb.Variants.Add("CoinCollect4", new());
                // bbb.Variants["CoinCollect4"] = new(){ Translation = new() { X = 0, Y = 100, Z = 0}, HiddenMeshes = ["BlockQuestionEmpty", "BlockQuestionTest"], 
                // Args = [ new AnimChangeArg() { AnimName = "Color", AnimType = H3DAnimation.MaterialAnim, Frame = 0, ID = 0 } ]   };
            
                // YAMLWrapper.Serialize<ModifierBase>(Path.Join(path, "CoinCollect4.yml"), bbb);
                foreach (string entryPath in Directory.EnumerateFiles(path))
                {
                    ModifierBase entry = YAMLWrapper.Deserialize<ModifierBase>(entryPath);
                    if (entry.Default != null && entry.Default.Value.Args != null)
                    {
                        entry.Default = SetEntryArgs(entry.Default.Value, path);
                    }
                    if (entry.Variants != null)
                    foreach (string ent in entry.Variants.Keys)
                    {           
                        if (entry.Variants[ent]!.Args == null) continue;
                        entry.Variants[ent] = SetEntryArgs(entry.Variants[ent], path);
                    }
                    s_ModifierEntries[Path.GetFileNameWithoutExtension(entryPath)] = entry;
                }
                ReloadEntries = false;
            }

            return s_ModifierEntries;
        }
    }

    public static ModifierEntry? GetEntry(string actorName, string className)
    {
        if (s_ModifierEntries is null || !s_ModifierEntries.ContainsKey(className))
        {
            return null;
        }

        if (s_ModifierEntries[className].Variants != null && s_ModifierEntries[className].Variants!.ContainsKey(actorName))
            return s_ModifierEntries[className].Variants![actorName];
        else if (s_ModifierEntries[className].Default != null)
            return s_ModifierEntries[className].Default;
        else
            return null;
    }

    private static ModifierEntry SetEntryArgs(ModifierEntry entry, string path)
    {
        entry.ArgsRem = [];
        foreach (string k in entry.Args!.Keys)
        {
            try
            {
                BaseArg arg = entry.Args[k].ArgType switch
                {
                    ArgType.Tower => new TowerArg(entry.Args[k].Properties),
                    ArgType.AnimChange => new AnimChangeArg(entry.Args[k].Properties),
                    ArgType.RotateCoreCount => new RotateCoreCount(),
                    ArgType.RotateCoreSides => new RotateCoreBars(entry.Args[k].Properties),
                    ArgType.SwingCoreLength => new SwingingCore(entry.Args[k].Properties),
                    ArgType.AddExtraModel => new ExtraArgModels(entry.Args[k].Properties),
                    ArgType.ShadowType => new ShadowType(),
                    ArgType.ScaleAxis => new ScaleAxis(entry.Args[k].Properties),
                    _ => new SimpleArg(),
                };
                entry.ArgsRem.Add(k, arg);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message} while reading Arg ${k} of entry {Path.GetFileName(path)}");
            }
        }
        return entry;
    }


}
