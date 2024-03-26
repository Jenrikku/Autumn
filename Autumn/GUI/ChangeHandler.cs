using Autumn.History;
using Autumn.Scene;

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
}
