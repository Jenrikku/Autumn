namespace Autumn.Scene.H3D.Animation;

// Based on: https://github.com/KillzXGaming/SPICA/blob/master/SPICA.Rendering/Animation/IAnimationControl.cs
internal interface IH3DAnimationControl
{
    public H3DAnimationState State { get; set; }

    public float Frame { get; set; }
    public float Step { get; set; }
    public float FramesCount { get; }
    public bool IsLooping { get; }

    public void AdvanceFrame();
    public void SlowDown();
    public void SpeedUp();
    public void Play(float Step = 1);
    public void Pause();
    public void Stop();
}

// Based on: https://github.com/KillzXGaming/SPICA/blob/master/SPICA.Rendering/Animation/AnimationState.cs
public enum H3DAnimationState
{
    Stopped,
    Paused,
    Playing
}
