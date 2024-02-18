namespace Autumn.History;

/// <summary>
/// A class that keeps track of any changes made and allows to undo and redo them.
/// </summary>
internal class ChangeHistory
{
    private readonly List<Change> _changes = new();
    private readonly List<Change> _undoneChanges = new();

    public bool CanUndo => _changes.Count > 0;
    public bool CanRedo => _undoneChanges.Count > 0;

    public void Add(Change change)
    {
        _changes.Add(change);
        _undoneChanges.Clear();
    }

    public void Add(Action undo, Action redo) => Add(new(undo, redo));

    public void Reset()
    {
        _changes.Clear();
        _undoneChanges.Clear();
    }

    public bool Undo()
    {
        int count = _changes.Count;

        if (count <= 0)
            return false;

        Change last = _changes[count - 1];

        last.Undo.Invoke();

        _changes.RemoveAt(count - 1);
        _undoneChanges.Add(last);

        return true;
    }

    public bool Redo()
    {
        int count = _undoneChanges.Count;

        if (count <= 0)
            return false;

        Change last = _undoneChanges[count - 1];

        last.Redo.Invoke();

        _undoneChanges.RemoveAt(count - 1);
        _changes.Add(last);

        return true;
    }
}
