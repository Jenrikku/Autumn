using System.Numerics;
using System.Reflection;
using Autumn.History;
using Autumn.Rendering;
using Autumn.Storage;

namespace Autumn.GUI;

internal static class ChangeHandler
{
    public static void ToggleObjectSelection(
        MainWindowContext context,
        ChangeHistory history,
        SceneObj obj,
        bool clear
    ) => ToggleObjectSelection(context, history, obj.PickingId, obj.GetType(), clear);

    public static void ToggleObjectSelection(
        MainWindowContext context,
        ChangeHistory history,
        uint id,
        Type type,
        bool clear
    )
    {
        SceneObj[]? cleared = null;
        if (context.CurrentScene is null) return;

        if (type == typeof(SceneObj))
        {

            bool isSelected = context.CurrentScene.IsObjectSelected(id);

            // If the only selected object is the one that has been clicked, then nothing is done.
            if (context.CurrentScene.SelectedObjects.Count() == 1 && isSelected && clear)
                return;

            if (clear)
            {
                cleared = context.CurrentScene.SelectedObjects.ToArray();

                if (context.CurrentScene.SelectedObjects.Count() > 1 && isSelected) // prevent it getting unselected when clicking on 1 of the multiselected
                    isSelected = false;

            }
            Change change =
                new(
                    Undo: () =>
                    {
                        if (cleared is not null)
                        {
                            context.CurrentScene.SetSelectedObjects(cleared);
                            return;
                        }

                        context.CurrentScene.SetObjectSelected(id, isSelected);
                    },
                    Redo: () =>
                    {
                        if (cleared is not null)
                            context.CurrentScene.UnselectAllObjects();

                        context.CurrentScene.SetObjectSelected(id, !isSelected);
                    }
                );

            change.Redo();
            history.Add(change);
        }
    }

    /// <summary>
    /// A method meant to be used to create changes of simple property value changes.
    /// </summary>
    /// <typeparam name="T1">The type of the object that has the property.</typeparam>
    /// <typeparam name="T2">The type of the value of the property.</typeparam>
    /// <param name="obj">The object that has the property.</param>
    /// <param name="name">The name of the property.</param>
    /// <param name="prior">The value that the property is set to when undoing.</param>
    /// <param name="final">The value that the property is set to when redoing.</param>
    /// <returns>Whether the change was added to the history successfully.</returns>
    public static bool ChangePropertyValue<T1, T2>(
        ChangeHistory history,
        T1 obj,
        string name,
        T2 prior,
        T2 final
    )
        where T1 : notnull
    {
        PropertyInfo? property = obj.GetType().GetProperty(name);

        if (
            property is null
            || !property.CanWrite
            || !property.PropertyType.IsAssignableFrom(typeof(T2))
        )
            return false;

        Change change =
            new(
                Undo: () => property.SetValue(obj, prior),
                Redo: () => property.SetValue(obj, final)
            );

        change.Redo();
        history.Add(change);
        return true;
    }

    // See method above.
    public static bool ChangeFieldValue<T1, T2>(
        ChangeHistory history,
        T1 obj,
        string name,
        T2 prior,
        T2 final
    )
        where T1 : notnull
    {
        FieldInfo? field = obj.GetType().GetField(name);

        if (field is null || !field.FieldType.IsAssignableFrom(typeof(T2)))
            return false;

        Change change =
            new(Undo: () => field.SetValue(obj, prior), Redo: () => field.SetValue(obj, final));

        change.Redo();
        history.Add(change);
        return true;
    }

    public static bool ChangeDictionaryValue<T1>(
        ChangeHistory history,
        Dictionary<string, T1> dict,
        string name,
        T1 old,
        T1 val
    )
    {
        Change change =
            new(
                Undo: () => { if (dict.ContainsKey(name)) dict[name] = old; },
                Redo: () => { if (dict.ContainsKey(name)) dict[name] = val; }
            );
        change.Redo();
        history.Add(change);
        return true;
    }
    // See method above.
    public static bool ChangeTransform(
        ChangeHistory history,
        SceneObj obj,
        string transform,
        Vector3 prior,
        Vector3 final
    )
    {
        FieldInfo? field = obj.StageObj.GetType().GetField(transform);
        if (field == null) return false;
        Change change =
        new(
            Undo: () =>
            {
                field.SetValue(obj.StageObj, prior);
                obj.UpdateTransform();
            },
            Redo: () =>
            {
                field.SetValue(obj.StageObj, final);
                obj.UpdateTransform();
            }
        );
        change.Redo();
        history.Add(change);
        return true;
    }


    public static bool ChangeMultiTransform(
        ChangeHistory history,
        Dictionary<SceneObj, Vector3> sobjL,
        string transform
    )
    {
        FieldInfo? field = new StageObj().GetType().GetField(transform); // These fields are there no matter what

        if (field == null) return false;
        List<Vector3> current = new();
        foreach (SceneObj obj in sobjL.Keys)
        {
            current.Add((Vector3)field.GetValue(obj.StageObj));
        }
        Change change =
        new(
            Undo: () =>
            {
                foreach (SceneObj obj in sobjL.Keys)
                {
                    field.SetValue(obj.StageObj, sobjL[obj]);
                    obj.UpdateTransform();
                }
            },
            Redo: () =>
            {
                int i = 0;
                foreach (SceneObj obj in sobjL.Keys)
                {
                    field.SetValue(obj.StageObj, current[i]);
                    obj.UpdateTransform();
                    i++;
                }
            }
        );
        history.Add(change);
        return true;
    }

}
