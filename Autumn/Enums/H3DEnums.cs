namespace Autumn.Enums;

internal enum H3DMeshLayer
{
    Opaque = 0,
    Translucent = 1,
    Subtractive = 2,
    Additive = 3
}

internal enum H3DAnimation
{
    SkeletalAnim = 0b1,
    MaterialAnim = 0b10,
    VisibilityAnim = 0b100,
}