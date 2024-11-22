using System.Reflection;
using Autumn.History;
using Autumn.Rendering;

namespace Autumn.GUI;

internal static class ChangeHandler
{
    public static void ToggleObjectSelection(
        MainWindowContext context,
        ChangeHistory history,
        SceneObj obj,
        bool clear
    ) => ToggleObjectSelection(context, history, obj.PickingId, clear);

    public static void ToggleObjectSelection(
        MainWindowContext context,
        ChangeHistory history,
        uint id,
        bool clear
    )
    {
        if (context.CurrentScene is null)
            return;

        SceneObj[]? cleared = null;
        bool isSelected = context.CurrentScene.IsObjectSelected(id);

        // If the only selected object is the one that has been clicked, then nothing is done.
        if (context.CurrentScene.SelectedObjects.Count() == 1 && isSelected)
            return;

        if (clear)
            cleared = context.CurrentScene.SelectedObjects.ToArray();

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
}
