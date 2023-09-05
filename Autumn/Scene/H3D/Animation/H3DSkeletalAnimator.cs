using Autumn.Utils;
using Silk.NET.Maths;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.Animation;
using SPICA.Formats.CtrH3D.Model;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace Autumn.Scene.H3D.Animation;

// Based on: https://github.com/KillzXGaming/SPICA/blob/master/SPICA.Rendering/Animation/SkeletalAnimation.cs
internal class H3DSkeletalAnimatior : H3DAnimationControl
{
    public class Bone
    {
        public Vector3 Translation = new();
        public Vector3 Scale = new(1);
        public Vector3 EulerRotation = new();
        public Bone? Parent = null;
        public int ParentIndex = -1;

        private Quaternion? _rotation = null;
        public Quaternion Rotation
        {
            get
            {
                if (!_rotation.HasValue)
                    CalculateQuaternion();

                return _rotation!.Value;
            }
            set => _rotation = value;
        }

        public void CalculateQuaternion()
        {
            double SX = Math.Sin(EulerRotation.X * 0.5f);
            double SY = Math.Sin(EulerRotation.Y * 0.5f);
            double SZ = Math.Sin(EulerRotation.Z * 0.5f);
            double CX = Math.Cos(EulerRotation.X * 0.5f);
            double CY = Math.Cos(EulerRotation.Y * 0.5f);
            double CZ = Math.Cos(EulerRotation.Z * 0.5f);

            double X = CZ * SX * CY - SZ * CX * SY;
            double Y = CZ * CX * SY + SZ * SX * CY;
            double Z = SZ * CX * CY - CZ * SX * SY;
            double W = CZ * CX * CY + SZ * SX * SY;

            _rotation = new((float)X, (float)Y, (float)Z, (float)W);
        }
    }

    private H3DDict<H3DBone> _skeleton;

    public Bone[] FrameSkeleton;

    private Matrix4x4[] _transforms;

    public H3DSkeletalAnimatior(H3DDict<H3DBone> skeleton)
    {
        _skeleton = skeleton;

        FrameSkeleton = new Bone[skeleton.Count];

        _transforms = new Matrix4x4[skeleton.Count];

        for (int i = 0; i < skeleton.Count; i++)
        {
            FrameSkeleton[i] = new() { ParentIndex = skeleton[i].ParentIndex };

            if (skeleton[i].ParentIndex != -1)
            {
                if (skeleton[i].ParentIndex >= i)
                    Debug.WriteLine("[H3DSkeletalAnimation] Skeleton is not properly sorted!");

                FrameSkeleton[i].Parent = FrameSkeleton[skeleton[i].ParentIndex];
            }
        }
    }

    public override void SetAnimations(IEnumerable<H3DAnimation> animations)
    {
        for (int i = 0; i < _skeleton.Count; i++)
        {
            FrameSkeleton[i].Scale = _skeleton[i].Scale;
            FrameSkeleton[i].EulerRotation = _skeleton[i].Rotation;
            FrameSkeleton[i].Translation = _skeleton[i].Translation;

            FrameSkeleton[i].CalculateQuaternion();
        }

        ResetTransforms();

        SetAnimations(animations, _skeleton);
    }

    public Matrix4x4[] GetSkeletonTransforms()
    {
        //if (State == H3DAnimationState.Stopped)
        //    ResetTransforms();

        int elementCount = Elements.Count;

        // if (State != H3DAnimationState.Playing || elementCount == 0)
        //     return _transforms;

        bool[] skips = new bool[_transforms.Length];

        for (int i = 0; i < elementCount; i++)
        {
            int index = Indices[i];
            Bone bone = FrameSkeleton[index];
            H3DAnimationElement element = Elements[i];

            switch (element.PrimitiveType)
            {
                case H3DPrimitiveType.Transform:
                    SetBone((H3DAnimTransform)element.Content, bone);
                    break;

                case H3DPrimitiveType.QuatTransform:
                    SetBone((H3DAnimQuatTransform)element.Content, bone);
                    break;

                case H3DPrimitiveType.MtxTransform:
                    H3DAnimMtxTransform mtxTransform = (H3DAnimMtxTransform)element.Content;

                    Matrix4X3<float> mtx = mtxTransform.GetTransform((int)Frame).ToSilkNetMtx();
                    MathUtils.Unpack3dTransformMatrix(in mtx, ref _transforms[index]);

                    skips[index] = true;

                    break;
            }
        }

        for (int i = 0; i < _transforms.Length; i++)
        {
            if (skips[i])
                continue;

            Bone bone = FrameSkeleton[i];

            bool scaleCompensate =
                (_skeleton[i].Flags & H3DBoneFlags.IsSegmentScaleCompensate) != 0;

            scaleCompensate &= bone.Parent != null;

            _transforms[i] = Matrix4x4.CreateScale(bone.Scale * 0.01f);
            _transforms[i] *= Matrix4x4.CreateFromQuaternion(bone.Rotation);
            _transforms[i] *= Matrix4x4.CreateTranslation(
                scaleCompensate ? bone.Translation * bone.Parent?.Scale ?? new(1) : bone.Translation
            );

            if (scaleCompensate)
            {
                _transforms[i] *= Matrix4x4.CreateScale(
                    1f / bone.Parent!.Scale.X,
                    1f / bone.Parent!.Scale.Y,
                    1f / bone.Parent!.Scale.Z
                );

                _transforms[i] *= _transforms[bone.ParentIndex];
            }
            else if (bone.Parent != null)
                _transforms[i] *= _transforms[bone.ParentIndex];
        }

        return _transforms;
    }

    private unsafe void ResetTransforms()
    {
        for (int i = 0; i < _skeleton.Count; i++)
        {
            Matrix4X3<float> mtx = _skeleton[i].InverseTransform.ToSilkNetMtx();

            Matrix4x4 inverseTranform = new();
            MathUtils.Unpack3dTransformMatrix(in mtx, ref inverseTranform);

            if (!Matrix4x4.Invert(inverseTranform, out _transforms[i]))
            {
                _transforms[i] = Matrix4x4.CreateScale(0.01f);
                continue;
            }
        }
    }

    private void SetBone(H3DAnimTransform Transform, Bone Bone)
    {
        TrySetFrameValue(Transform.ScaleX, ref Bone.Scale.X);
        TrySetFrameValue(Transform.ScaleY, ref Bone.Scale.Y);
        TrySetFrameValue(Transform.ScaleZ, ref Bone.Scale.Z);

        TrySetFrameValue(Transform.RotationX, ref Bone.EulerRotation.X);
        TrySetFrameValue(Transform.RotationY, ref Bone.EulerRotation.Y);
        TrySetFrameValue(Transform.RotationZ, ref Bone.EulerRotation.Z);

        TrySetFrameValue(Transform.TranslationX, ref Bone.Translation.X);
        TrySetFrameValue(Transform.TranslationY, ref Bone.Translation.Y);
        TrySetFrameValue(Transform.TranslationZ, ref Bone.Translation.Z);

        if (Transform.RotationExists)
            Bone.CalculateQuaternion();
    }

    private void TrySetFrameValue(H3DFloatKeyFrameGroup group, ref float value)
    {
        if (group.Exists)
            value = group.GetFrameValue(Frame);
    }

    private void SetBone(H3DAnimQuatTransform transform, Bone bone)
    {
        int frame = (int)Frame;

        if (frame != Frame)
            SetBone(transform, bone, frame, Frame - frame);
        else
            SetBone(transform, bone, frame);
    }

    private static void SetBone(H3DAnimQuatTransform transform, Bone bone, int frame, float weight)
    {
        if (transform.HasScale)
            bone.Scale = Vector3.Lerp(
                transform.GetScaleValue(frame + 0),
                transform.GetScaleValue(frame + 1),
                weight
            );

        if (transform.HasRotation)
            bone.Rotation = Quaternion.Slerp(
                transform.GetRotationValue(frame + 0),
                transform.GetRotationValue(frame + 1),
                weight
            );

        if (transform.HasTranslation)
            bone.Translation = Vector3.Lerp(
                transform.GetTranslationValue(frame + 0),
                transform.GetTranslationValue(frame + 1),
                weight
            );
    }

    private static void SetBone(H3DAnimQuatTransform transform, Bone bone, int frame)
    {
        if (transform.HasScale)
            bone.Scale = transform.GetScaleValue(frame);

        if (transform.HasRotation)
            bone.Rotation = transform.GetRotationValue(frame);

        if (transform.HasTranslation)
            bone.Translation = transform.GetTranslationValue(frame);
    }
}
