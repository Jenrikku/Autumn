using Autumn.Storage;

namespace Autumn.Rendering.Storage;

internal interface IStageSceneObj : ISceneObj
{
    public StageObj StageObj { get; }    
}
