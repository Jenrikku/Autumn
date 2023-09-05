using SPICA.Formats.Common;
using SPICA.Formats.CtrH3D.Animation;

namespace Autumn.Scene.H3D.Animation;

// Based on: https://github.com/KillzXGaming/SPICA/blob/master/SPICA.Rendering/Animation/AnimationControl.cs
internal class H3DAnimationControl : IH3DAnimationControl
{
    public H3DAnimationState State { get; set; }

    private float _frame;

    public float Frame
    {
        get { return _frame; }
        set { _frame = value > FramesCount ? value % FramesCount : value; }
    }

    public float Step { get; set; }
    public float FramesCount { get; protected set; }
    public bool IsLooping { get; protected set; }

    protected List<int> Indices;

    protected List<H3DAnimationElement> Elements;

    protected List<string> TextureNames;

    public H3DAnimationControl()
    {
        Step = 1;
        Indices = new();
        Elements = new();
        TextureNames = new();
    }

    public virtual void SetAnimations(IEnumerable<H3DAnimation> Animations) { }

    protected void SetAnimations(IEnumerable<H3DAnimation> Animations, INameIndexed Dictionary)
    {
        Indices.Clear();
        Elements.Clear();

        float frameCount = 0;

        HashSet<string> usedNames = new();

        TextureNames.Clear();

        foreach (H3DAnimation anim in Animations)
        {
            if (frameCount < anim.FramesCount)
                frameCount = anim.FramesCount;

            if (anim is H3DMaterialAnim matAnim)
            {
                if (TextureNames.Count < matAnim.TextureNames.Count)
                    TextureNames = new List<string>(matAnim.TextureNames);
            }

            foreach (H3DAnimationElement element in anim.Elements)
            {
                if (usedNames.Contains(element.Name))
                    continue;

                usedNames.Add(element.Name);

                int index = Dictionary.Find(element.Name);

                if (index != -1)
                {
                    Indices.Add(index);
                    Elements.Add(element);
                }
            }
        }

        FramesCount = frameCount;
    }

    public void AdvanceFrame()
    {
        if (FramesCount >= Math.Abs(Step) && State == H3DAnimationState.Playing)
        {
            _frame += Step;

            if (_frame < 0)
            {
                _frame += FramesCount;
            }
            else if (_frame >= FramesCount)
            {
                _frame -= FramesCount;
            }
        }
    }

    public void SlowDown()
    {
        if (State == H3DAnimationState.Playing && Math.Abs(Step) > 0.125f)
            Step *= 0.5f;
    }

    public void SpeedUp()
    {
        if (State == H3DAnimationState.Playing && Math.Abs(Step) < 8)
            Step *= 2;
    }

    public void Play(float step = 1)
    {
        Step = step;

        State = H3DAnimationState.Playing;
    }

    public void Pause()
    {
        State = H3DAnimationState.Paused;
    }

    public void Stop()
    {
        State = H3DAnimationState.Stopped;

        _frame = 0;
    }
}
