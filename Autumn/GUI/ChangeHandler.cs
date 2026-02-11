using System.Numerics;
using System.Reflection;
using Autumn.Context;
using Autumn.Enums;
using Autumn.GUI.Windows;
using Autumn.History;
using Autumn.Rendering.Area;
using Autumn.Rendering.Storage;
using Autumn.Storage;

namespace Autumn.GUI;

internal static class ChangeHandler
{
    public static void ToggleObjectSelection(MainWindowContext context, ChangeHistory history, uint id, bool clear)
    {
        ISceneObj[]? cleared = null;
        if (context.CurrentScene is null)
            return;

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
        // if (context.CurrentScene.TryGetPickableObj(id, out ISceneObj? sceneObj))
        // {
        //     if (sceneObj is RailSceneObj && context.CurrentScene.SelectedObjects.Where(x => x is RailPointSceneObj || x is RailHandleSceneObj).Count() > 0)
        //     {
        //         context.CurrentScene.SelectedObjects.
        //     }
        //     else if (sceneObj is RailPointSceneObj && context.CurrentScene.SelectedObjects.Where(x => x is RailHandleSceneObj).Count() > 0)
        //     {

        //     }
        // }

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
    public static bool ChangePropertyValue<T1, T2>(ChangeHistory history, T1 obj, string name, T2 prior, T2 final)
        where T1 : notnull
    {
        PropertyInfo? property = obj.GetType().GetProperty(name);

        if (property is null || !property.CanWrite || !property.PropertyType.IsAssignableFrom(typeof(T2)))
            return false;

        Change change = new(Undo: () => property.SetValue(obj, prior), Redo: () => property.SetValue(obj, final));

        change.Redo();
        history.Add(change);
        return true;
    }

    // See method above.
    public static bool ChangeFieldValue<T1, T2>(ChangeHistory history, T1 obj, string name, T2 prior, T2 final)
        where T1 : notnull
    {
        FieldInfo? field = obj.GetType().GetField(name);

        if (field is null || !field.FieldType.IsAssignableFrom(typeof(T2)))
            return false;

        Change change = new(Undo: () => field.SetValue(obj, prior), Redo: () => field.SetValue(obj, final));

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangeActorName(MainWindowContext window, ChangeHistory history, IStageSceneObj sceneObj, string prior, string final)
    {
        if (prior == final) return false;
        Change change = new(
            Undo: () => 
            {
                if (sceneObj is ActorSceneObj actorSceneObj)
                {
                    actorSceneObj.StageObj.Name = prior;
                    actorSceneObj.UpdateActor(window.ContextHandler.FSHandler, window.GLTaskScheduler);
                }
                if (sceneObj is BasicSceneObj basicSceneObj && basicSceneObj.StageObj.IsArea())
                {
                    basicSceneObj.StageObj.Name = prior;
                    basicSceneObj.MaterialParams.Color = AreaMaterial.GetAreaColor(basicSceneObj.StageObj.Name);
                }
            }, 
            Redo: () => 
            {
                if (sceneObj is ActorSceneObj actorSceneObj)
                {
                    actorSceneObj.StageObj.Name = final;
                    actorSceneObj.UpdateActor(window.ContextHandler.FSHandler, window.GLTaskScheduler);
                }
                if (sceneObj is BasicSceneObj basicSceneObj && basicSceneObj.StageObj.IsArea())
                {
                    basicSceneObj.StageObj.Name = final;
                    basicSceneObj.MaterialParams.Color = AreaMaterial.GetAreaColor(basicSceneObj.StageObj.Name);
                }
            }
            );
        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangeFieldValueMultiple<T>(
        ChangeHistory history,
        IEnumerable<ISceneObj> sList,
        string name,
        T nvar
    )
        where T : notnull
    {
        FieldInfo? field = sList.FirstOrDefault()?.GetType().GetField(name);

        if (field is null)
            return false;

        List<T> current = new();
        foreach (ISceneObj obj in sList)
        {
            current.Add((T)field.GetValue(obj)!);
        }

        Change change =
        new(
            Undo: () =>
            {
                int i = 0;
                foreach (ISceneObj obj in sList)
                {
                    field.SetValue(obj, current[i]);
                    i++;
                }
            },
            Redo: () =>
            {
                int i = 0;
                foreach (ISceneObj obj in sList)
                {
                    field.SetValue(obj, nvar);
                    i++;
                }
            }
        );
        change.Redo();
        history.Add(change);
        return true;
    }

    public static bool ChangeHideMultiple(
        ChangeHistory history,
        IEnumerable<ISceneObj> sList
    )
    {
        List<bool> current = new();
        foreach (ISceneObj obj in sList)
        {
            current.Add(obj.IsVisible);
        }
        Change change =
        new(
            Undo: () =>
            {
                int i = 0;
                foreach (ISceneObj obj in sList)
                {
                    obj.IsVisible =  current[i];
                    i++;
                }
            },
            Redo: () =>
            {
                int i = 0;
                foreach (ISceneObj obj in sList)
                {
                    obj.IsVisible = !current[i];
                    i++;
                }
            }
        );

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
                Undo: () =>
                {
                    if (dict.ContainsKey(name))
                        dict[name] = old;
                },
                Redo: () =>
                {
                    if (dict.ContainsKey(name))
                        dict[name] = val;
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }

    // See method above.
    public static bool ChangeStageObjTransform(
        ChangeHistory history,
        IStageSceneObj obj,
        string transform,
        Vector3 prior,
        Vector3 final
    )
    {
        FieldInfo? field = obj.StageObj.GetType().GetField(transform);

        if (field is null)
            return false;

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

    public static bool ChangeRailScale(
        ChangeHistory history,
        RailSceneObj obj,
        Vector3 prior,
        Vector3 final
    )
    {
        final.X = final.X == 0 ? 0.01f : final.X;
        final.Y = final.Y == 0 ? 0.01f : final.Y;
        final.Z = final.Z == 0 ? 0.01f : final.Z;
        Change change =
            new(
                Undo: () =>
                {
                    obj.FakeScale = Vector3.One / final;
                    obj.UpdateAfterScale();
                },
                Redo: () =>
                {
                    obj.FakeScale = final;
                    obj.UpdateAfterScale();
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangePointRot(
        ChangeHistory history,
        RailPointSceneObj obj,
        Vector3 final
    )
    {
        Change change =
            new(
                Undo: () =>
                {
                    obj.FakeRotation = Vector3.Zero - final;
                    obj.UpdateTransform();
                },
                Redo: () =>
                {
                    obj.FakeRotation = final;
                    obj.UpdateTransform();
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangeRailRot(
        ChangeHistory history,
        RailSceneObj obj,
        Vector3 final
    )
    {
        Change change =
            new(
                Undo: () =>
                {
                    obj.FakeRotation = Vector3.Zero - final;
                    obj.UpdateAfterRotate();
                },
                Redo: () =>
                {
                    obj.FakeRotation = final;
                    obj.UpdateAfterRotate();
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }

    public static bool ChangeHandleTransform(
        ChangeHistory history,
        RailHandleSceneObj obj,
        Vector3 prior,
        Vector3 final,
        bool resetTransform = true
    )
    {
        Change change =
            new(
                Undo: () =>
                {
                    obj.Offset = prior +(resetTransform ? - obj.ParentPoint.RailPoint.Point0Trans : Vector3.Zero);
                    obj.UpdateTransform();
                },
                Redo: () =>
                {
                    obj.Offset = final;
                    obj.UpdateTransform();
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangeRailPosition(
        ChangeHistory history,
        RailSceneObj obj,
        Vector3 prior,
        Vector3 final
    )
    {
        Change change =
            new(
                Undo: () =>
                {
                    obj.FakeOffset = -final;
                    obj.UpdateAfterMove();
                },
                Redo: () =>
                {
                    obj.FakeOffset = final;
                    obj.UpdateAfterMove();
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangePointPosition(
        ChangeHistory history,
        RailPointSceneObj obj,
        Vector3 prior,
        Vector3 final,
        bool KeepHandles
    )
    {
        Vector3 Difference = final-prior;
        bool first = KeepHandles;
        Change change =
            new(
                Undo: () =>
                {
                    obj.RailPoint.Point0Trans = prior;
                    obj.UpdateModelMoving();
                },
                Redo: () =>
                {
                    obj.RailPoint.Point0Trans = final;
                    obj.UpdateModelMoving();
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangeUnlinkChild(
        MainWindowContext window,
        ChangeHistory history,
        StageObj child
    )
    {
        StageObj parent = child.Parent!;
        Change change =
            new(
                Undo: () =>
                {   
                    window.CurrentScene?.Stage.GetStageFile(StageFileType.Map).SetChild(child, parent);
                },
                Redo: () =>
                {
                    window.CurrentScene?.Stage.GetStageFile(StageFileType.Map).UnlinkChild(child);
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangeSetChild(
        MainWindowContext window,
        ChangeHistory history,
        StageObj child,
        StageObj parent
    )
    {
        var oldParent = child.Parent;
        Change change =
            new(
                Undo: () =>
                {   
                    if (oldParent != null)
                        window.CurrentScene?.Stage.GetStageFile(StageFileType.Map).SetChild(child, oldParent);
                    else
                        window.CurrentScene?.Stage.GetStageFile(StageFileType.Map).UnlinkChild(child);
                },
                Redo: () =>
                {
                    window.CurrentScene?.Stage.GetStageFile(StageFileType.Map).SetChild(child, parent);
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }

    public static bool ChangeMultiTransform(
        ChangeHistory history,
        Dictionary<ISceneObj, Vector3> sobjL,
        string transform
    )
    {
        FieldInfo? field = new StageObj().GetType().GetField(transform); // These fields are there no matter what

        if (field is null)
            return false;

        List<Vector3> current = new();
        foreach (IStageSceneObj obj in sobjL.Keys)
        {
            current.Add((Vector3)field.GetValue(obj.StageObj)!);
        }

        Change change =
            new(
                Undo: () =>
                {
                    foreach (IStageSceneObj obj in sobjL.Keys)
                    {
                        field.SetValue(obj.StageObj, sobjL[obj]);
                        obj.UpdateTransform();
                    }
                },
                Redo: () =>
                {
                    int i = 0;
                    foreach (IStageSceneObj obj in sobjL.Keys)
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

    public static bool ChangeMultiScale(
        ChangeHistory history,
        Dictionary<ISceneObj, Vector3> originals,
        Dictionary<ISceneObj, Vector3> news
    )
    {
        List<Vector3> current = new();
        foreach (ISceneObj obj in news.Keys)
        {
            if (obj is RailSceneObj)
            {
                Vector3 aa = news[obj];   
                aa.X = news[obj].X == 0 ? 0.01f : news[obj].X;
                aa.Y = news[obj].Y == 0 ? 0.01f : news[obj].Y;
                aa.Z = news[obj].Z == 0 ? 0.01f : news[obj].Z;
                news[obj] = aa;
            }
            current.Add(news[obj]);
        }

        Change change =
            new(
                Undo: () =>
                {
                    foreach (ISceneObj obj in news.Keys)
                    {
                        switch (obj)
                        {
                            case ISceneObj x when x is IStageSceneObj y:
                                y.StageObj.Scale = originals[obj];
                                obj.UpdateTransform();
                            break;
                            case ISceneObj x when x is RailSceneObj y:
                                y.FakeScale = Vector3.One / news[obj];
                                y.UpdateAfterScale();
                            break;
                            case ISceneObj x when x is RailPointSceneObj y:
                            break;
                            case ISceneObj x when x is RailHandleSceneObj y:
                            break;

                        }
                    }
                },
                Redo: () =>
                {
                    int i = 0;
                    foreach (ISceneObj obj in news.Keys)
                    {
                        switch (obj)
                        {
                            case ISceneObj x when x is IStageSceneObj y:
                                y.StageObj.Scale = news[obj];
                                obj.UpdateTransform();
                            break;
                            case ISceneObj x when x is RailSceneObj y:
                                y.FakeScale = news[obj];
                                y.UpdateAfterScale();
                            break;
                            case ISceneObj x when x is RailPointSceneObj y:
                            break;
                            case ISceneObj x when x is RailHandleSceneObj y:
                            break;
                        }
                        i++;
                    }
                }
            );
        change.Redo();
        history.Add(change);
        return true;
    }

    public static bool ChangeMultiRotate(
        ChangeHistory history,
        Dictionary<ISceneObj, Vector3> originals,
        Dictionary<ISceneObj, Vector3> news
    )
       {
        //List<Vector3> old = new();
        List<Vector3> current = new();
        foreach (ISceneObj obj in news.Keys)
        {
            current.Add(news[obj]);
            //old.Add(originals[obj]);
        }

        Change change =
            new(
                Undo: () =>
                {
                    foreach (ISceneObj obj in news.Keys)
                    {
                        switch (obj)
                        {
                            case ISceneObj x when x is IStageSceneObj y:
                                y.StageObj.Rotation = originals[obj];
                                obj.UpdateTransform();


                            break;
                            case ISceneObj x when x is RailSceneObj y:
                                y.FakeRotation = Vector3.Zero - news[obj];
                                y.UpdateAfterRotate();
                            break;
                            case ISceneObj x when x is RailPointSceneObj y:
                                y.FakeRotation = Vector3.Zero - news[obj];
                                obj.UpdateTransform();
                            break;
                            case ISceneObj x when x is RailHandleSceneObj y:
                                // y.Offset = originals[obj] + (/*resetTransform*/true ? - y.ParentPoint.RailPoint.Point0Trans : Vector3.Zero);
                                // y.UpdateTransform();
                            break;

                        }
                    }
                },
                Redo: () =>
                {
                    int i = 0;
                    foreach (ISceneObj obj in news.Keys)
                    {
                        switch (obj)
                        {
                            case ISceneObj x when x is IStageSceneObj y:
                                y.StageObj.Rotation = news[obj];
                                obj.UpdateTransform();

                            break;
                            case ISceneObj x when x is RailSceneObj y:
                                y.FakeRotation = news[obj];
                                y.UpdateAfterRotate();
                            break;
                            case ISceneObj x when x is RailPointSceneObj y:
                                y.FakeRotation = news[obj];
                                obj.UpdateTransform();
                            break;
                            case ISceneObj x when x is RailHandleSceneObj y:
                                // y.Offset = news[obj] + (-y.ParentPoint.RailPoint.Point0Trans);
                                // y.UpdateTransform();
                            break;
                        }
                        i++;
                    }
                }
            );
        change.Redo();
        history.Add(change);
        return true;
    }


    public static bool ChangeMultiMove(
        ChangeHistory history,
        Dictionary<ISceneObj, Vector3> originals,
        Dictionary<ISceneObj, Vector3> news
    )
    {
        //List<Vector3> old = new();
        List<Vector3> current = new();
        foreach (ISceneObj obj in news.Keys)
        {
            current.Add(news[obj]);
            //old.Add(originals[obj]);
        }

        Change change =
            new(
                Undo: () =>
                {
                    foreach (ISceneObj obj in news.Keys)
                    {
                        switch (obj)
                        {
                            case ISceneObj x when x is IStageSceneObj y:
                                y.StageObj.Translation = originals[obj];
                                obj.UpdateTransform();
                            break;
                            case ISceneObj x when x is RailSceneObj y:
                                y.FakeOffset = -news[obj];
                                y.UpdateAfterMove();
                            break;
                            case ISceneObj x when x is RailPointSceneObj y:
                                y.RailPoint.Point0Trans  = originals[obj];
                                y.RailPoint.Point1Trans -= (news[obj] - originals[obj]);
                                y.RailPoint.Point2Trans -= (news[obj] - originals[obj]);
                                obj.UpdateTransform();
                            break;
                            case ISceneObj x when x is RailHandleSceneObj y:
                                y.Offset = originals[obj] + (/*resetTransform*/true ? - y.ParentPoint.RailPoint.Point0Trans : Vector3.Zero);
                                y.UpdateTransform();
                            break;

                        }
                    }
                },
                Redo: () =>
                {
                    int i = 0;
                    foreach (ISceneObj obj in news.Keys)
                    {
                        switch (obj)
                        {
                            case ISceneObj x when x is IStageSceneObj y:
                                y.StageObj.Translation = news[obj];
                                obj.UpdateTransform();
                            break;
                            case ISceneObj x when x is RailSceneObj y:
                                y.FakeOffset = news[obj];
                                y.UpdateAfterMove();
                            break;
                            case ISceneObj x when x is RailPointSceneObj y:
                                y.RailPoint.Point0Trans  = news[obj];
                                //y.RailPoint.Point1Trans += (news[obj] - originals[obj]);
                                //y.RailPoint.Point2Trans += (news[obj] - originals[obj]);
                                y.UpdateModelMoving();
                            break;
                            case ISceneObj x when x is RailHandleSceneObj y:
                                y.Offset = news[obj] + (-y.ParentPoint.RailPoint.Point0Trans);
                                y.UpdateTransform();
                            break;
                        }
                        i++;
                    }
                }
            );
        change.Redo();
        history.Add(change);
        return true;
    }

    /// <summary>
    /// This always adds the point at the end of the list
    /// </summary>
    /// <param name="context"></param>
    /// <param name="history"></param>
    /// <param name="del"></param>
    /// <returns></returns>
    public static bool ChangeAddPoint(MainWindowContext context, ChangeHistory history, RailSceneObj rl, Vector3 initialPos)
    {
        uint pick = 0;

        Change change =
            new(
                Undo: () =>
                {
                    context.CurrentScene?.RemovePointRail(rl, pick);
                },
                Redo: () =>
                {
                    if (context.CurrentScene is null)
                        return;

                    context.CurrentScene.AddPointRail(rl, initialPos);
                    pick = rl.RailPoints.Last().PickingId;
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    public static bool ChangeInsertPoint(MainWindowContext context, ChangeHistory history, RailSceneObj rl, int pos, Vector3 initialPos)
    {
        uint pick = 0;

        Change change =
            new(
                Undo: () =>
                {
                    context.CurrentScene?.RemovePointRail(rl, pick);
                },
                Redo: () =>
                {
                    if (context.CurrentScene is null)
                        return;

                    pick = context.CurrentScene.InsertPointRail(rl, pos+1, initialPos);
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }
    /// <summary>
    /// This tries to add the point back to its original position
    /// </summary>
    /// <param name="context"></param>
    /// <param name="history"></param>
    /// <param name="Point"></param>
    /// <returns></returns>
    public static bool ChangeRemovePoint(MainWindowContext context, ChangeHistory history, RailPointSceneObj Point)
    {
        var PointClone = Point;
        var parent = Point.ParentRail;
        uint pick = PointClone.PickingId;
        int pos = parent.RailPoints.IndexOf(Point);
        bool deleted = false;

        Change change =
            new(
                Undo: () =>
                {
                    if (context.CurrentScene is null)
                        return;

                    if (deleted)
                    {
                        context.CurrentScene.ReAddObject(Point.ParentRail.RailObj, context.ContextHandler.FSHandler, context.GLTaskScheduler);
                        parent = context.CurrentScene.EnumerateRailSceneObjs().Last();
                        pick = parent.RailPoints[0].PickingId;
                        deleted = false;
                    }
                    else
                        pick = context.CurrentScene.InsertPointRail(context.CurrentScene.EnumerateRailSceneObjs().First(x => x.RailObj == parent.RailObj), pos, PointClone.RailPoint.Point0Trans, PointClone.RailPoint.Point1Trans, PointClone.RailPoint.Point2Trans);
                    
                    Console.WriteLine(pick);
                    Console.WriteLine(history.RedoSteps);
                    Console.WriteLine(history.UndoSteps);
                },
                Redo: () =>
                {
                    if (parent.RailPoints.Count < 2)
                    {
                        context.CurrentScene?.RemoveObject(parent);
                        deleted = true;
                    }
                    else
                        context.CurrentScene?.RemovePointRail(parent, pick);
                    Console.WriteLine(pick);
                    Console.WriteLine(history.RedoSteps);
                    Console.WriteLine(history.UndoSteps);
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }

    public static bool ChangeRemove(MainWindowContext context, ChangeHistory history, ISceneObj del)
    {

        var oldSO = del is IStageSceneObj delSt ? delSt.StageObj.Clone() : (del as RailSceneObj)!.RailObj.Clone();
        var delete = del;

        Change change =
            new(
                Undo: () =>
                {
                    if (context.CurrentScene is null)
                        return;

                    context.CurrentScene.ReAddObject(oldSO, context.ContextHandler.FSHandler, context.GLTaskScheduler);
                    if (delete is IStageSceneObj) delete = context.CurrentScene.EnumerateStageSceneObjs().Last();
                    else if (delete is RailSceneObj) delete = context.CurrentScene.EnumerateRailSceneObjs().Last();
                },
                Redo: () =>
                {
                    context.CurrentScene?.RemoveObject(delete);
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }

    public static bool ChangeCreate(MainWindowContext context, ChangeHistory history, StageObj newObj)
    {
        ISceneObj? delete = null;
        bool isRail = newObj is RailObj;

        Change change =
            new(
                Undo: () =>
                {
                    context.CurrentScene?.RemoveObject(delete!);
                },
                Redo: () =>
                {
                    if (context.CurrentScene is null)
                        return;

                    context.CurrentScene.AddObject(newObj, context.ContextHandler.FSHandler, context.GLTaskScheduler);
                    if (isRail) delete = context.CurrentScene.EnumerateRailSceneObjs().Last();
                    else delete = context.CurrentScene.EnumerateStageSceneObjs().Last();
                }
            );

        change.Redo();
        history.Add(change);
        return true;
    }

    public static uint ChangeDuplicate(
        MainWindowContext context,
        ChangeHistory history,
        ISceneObj dup,
        bool keepChildren = false
    )
    {
        var duplicate = dup;
        uint return_pick = 0;
        Change change = new(
                    Undo: () =>
                    {
                        context.CurrentScene?.SetObjectSelected(duplicate.PickingId, false);
                        context.CurrentScene?.RemoveObject(duplicate);
                    },
                    Redo: () => 
                    {
                        if (context.CurrentScene is null)
                            return;

                        if (dup is IStageSceneObj)
                        {
                        StageObj clone = (duplicate as IStageSceneObj)!.StageObj.Clone();

                        return_pick = context.CurrentScene.DuplicateObj(
                            clone,
                            context.ContextHandler.FSHandler,
                            context.GLTaskScheduler
                        );
                        duplicate = context.CurrentScene.EnumerateStageSceneObjs().Last();
                        }
                        else if (dup is RailSceneObj)
                        {
                        RailObj clone = (duplicate as RailSceneObj)!.RailObj.Clone();

                        return_pick = context.CurrentScene.DuplicateObj(
                            clone,
                            context.ContextHandler.FSHandler,
                            context.GLTaskScheduler
                        );
                        duplicate = context.CurrentScene.EnumerateRailSceneObjs().Last();
                        return_pick = duplicate.PickingId;
                        }
                    }
        );
        change.Redo();
        history.Add(change);
        return return_pick;
    }
}
