using Autumn.History;
using Autumn.Scene;

namespace Autumn.GUI;

internal class ChangeHandler
{
    public ChangeHistory History { get; } = new();

    public void ToggleObjectSelection(MainWindowContext context, SceneObj obj, bool clear) =>
        ToggleObjectSelection(context, obj.PickingId, clear);

    public void ToggleObjectSelection(MainWindowContext context, uint id, bool clear)
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
        History.Add(change);
    }
}
