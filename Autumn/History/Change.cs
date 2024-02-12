namespace Autumn.History;

internal record Change(Action Undo, Action Redo);
