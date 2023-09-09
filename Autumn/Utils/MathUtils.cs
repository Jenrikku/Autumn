using Silk.NET.Maths;
using SPICA.Formats.CtrH3D.Model.Material;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Autumn.Utils;

internal static class MathUtils
{
    // Based on SceneGL.Testing: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/Util/NumericsUtil.cs
    /// <summary>
    /// Creates a perspective projection matrix based on a field of view, aspect ratio, and near and far view plane distances.
    /// </summary>
    /// <param name="fieldOfView">Field of view in the y direction, in radians.</param>
    /// <param name="aspectRatio">Aspect ratio, defined as view space width divided by height.</param>
    /// <param name="nearPlaneDistance">Distance to the near view plane.</param>
    /// <param name="farPlaneDistance">Distance to the far view plane.</param>
    /// <returns>The perspective projection matrix.</returns>
    public static Matrix4x4 CreatePerspectiveReversedDepth(
        float fieldOfView,
        float aspectRatio,
        float nearPlaneDistance
    )
    {
        if (fieldOfView <= 0.0f || fieldOfView >= Math.PI)
            throw new ArgumentOutOfRangeException("fieldOfView");

        if (nearPlaneDistance <= 0.0f)
            throw new ArgumentOutOfRangeException("nearPlaneDistance");

        float yScale = 1.0f / (float)Math.Tan(fieldOfView * 0.5f);
        float xScale = yScale / aspectRatio;

        Matrix4x4 result;

        result.M11 = xScale;
        result.M12 = result.M13 = result.M14 = 0.0f;

        result.M22 = yScale;
        result.M21 = result.M23 = result.M24 = 0.0f;

        result.M31 = result.M32 = result.M33 = 0.0f;
        result.M34 = -1.0f;

        result.M41 = result.M42 = result.M44 = 0.0f;
        result.M43 = nearPlaneDistance;

        return result;
    }

    public static Matrix4x4 CreatePerspectiveFieldOfView(
        float fovy,
        float aspect,
        float zNear,
        float zFar
    )
    {
        if (zNear <= 0)
            throw new ArgumentOutOfRangeException("zNear");
        if (zFar <= 0)
            throw new ArgumentOutOfRangeException("zFar");
        if (zNear >= zFar)
            throw new ArgumentOutOfRangeException("zNear");

        float yMax = zNear * (float)System.Math.Tan(0.5f * fovy);
        float yMin = -yMax;
        float xMin = yMin * aspect;
        float xMax = yMax * aspect;

        float x = (2.0f * zNear) / (xMax - xMin);
        float y = (2.0f * zNear) / (yMax - yMin);
        float a = (xMax + xMin) / (xMax - xMin);
        float b = (yMax + yMin) / (yMax - yMin);
        float c = -(zFar + zNear) / (zFar - zNear);
        float d = -(2.0f * zFar * zNear) / (zFar - zNear);

        return new Matrix4x4(x, 0, 0, 0, 0, y, 0, 0, a, b, c, -1, 0, 0, d, 0);
    }

    /// <summary>
    /// Packs the given transform matrix into a row_major 4x3 matrix for packing in a uniform buffer
    /// <para>Will only work reliably if the uniform block has <code>layout (std140, row_major)</code></para>
    /// </summary>
    /// <param name="transform"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pack3dTransformMatrix(in Matrix4x4 transform, ref Matrix3X4<float> dest)
    {
        dest.M11 = transform.M11;
        dest.M21 = transform.M12;
        dest.M31 = transform.M13;

        dest.M12 = transform.M21;
        dest.M22 = transform.M22;
        dest.M32 = transform.M23;

        dest.M13 = transform.M31;
        dest.M23 = transform.M32;
        dest.M33 = transform.M33;

        dest.M14 = transform.M41;
        dest.M24 = transform.M42;
        dest.M34 = transform.M43;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Unpack3dTransformMatrix(in Matrix4X3<float> transform, ref Matrix4x4 dest)
    {
        dest.M11 = transform.M11;
        dest.M12 = transform.M12;
        dest.M13 = transform.M13;

        dest.M21 = transform.M21;
        dest.M22 = transform.M22;
        dest.M23 = transform.M23;

        dest.M31 = transform.M31;
        dest.M32 = transform.M32;
        dest.M33 = transform.M33;

        dest.M41 = transform.M41;
        dest.M42 = transform.M42;
        dest.M43 = transform.M43;

        dest.M44 = 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TransformToMatrix2X4(in Matrix4x4 transform, ref Matrix2X4<float> dest)
    {
        dest.M11 = transform.M11;
        dest.M21 = transform.M12;

        dest.M12 = transform.M21;
        dest.M22 = transform.M22;

        dest.M13 = transform.M31;
        dest.M23 = transform.M32;

        dest.M14 = transform.M41;
        dest.M24 = transform.M42;
    }

    public static Matrix4x4 CreateTransform(Vector3 translation, Vector3 scale, Vector3 rotation)
    {
        float rotX = (float)(Math.PI / 180 * rotation.X),
            rotY = (float)(Math.PI / 180 * rotation.Y),
            rotZ = (float)(Math.PI / 180 * rotation.Z);

        // M = T * S * (Rx * Ry * Rz)
        // Where T -> Translation, S -> Scale, R -> Rotation.

        Matrix4x4 mTranslation = Matrix4x4.CreateTranslation(translation);
        Matrix4x4 mScale = Matrix4x4.CreateScale(scale);

        Matrix4x4 mRotationX = Matrix4x4.CreateRotationX(rotX);
        Matrix4x4 mRotationY = Matrix4x4.CreateRotationY(rotY);
        Matrix4x4 mRotationZ = Matrix4x4.CreateRotationZ(rotZ);

        return mScale * (mRotationX * mRotationY * mRotationZ) * mTranslation;

        //return new() {
        //    M11 = (float) (scale.X * (Math.Cos(rotY) * Math.Cos(rotZ))),
        //    M12 = (float) (scale.X * (Math.Cos(rotY) * Math.Sin(rotZ))),
        //    M13 = (float) (scale.X * -Math.Sin(rotY)),
        //    M14 = translation.X,

        //    M21 = (float) (scale.Y * (Math.Sin(rotX) * Math.Sin(rotY) * Math.Cos(rotZ) - Math.Cos(rotX) * Math.Sin(rotZ))),
        //    M22 = (float) (scale.Y * (Math.Sin(rotX) * Math.Sin(rotY) * Math.Sin(rotZ) + Math.Cos(rotX) * Math.Cos(rotZ))),
        //    M23 = (float) (scale.Y * (Math.Sin(rotX) * Math.Cos(rotY))),
        //    M24 = translation.Y,

        //    M31 = (float) (scale.Z * (Math.Cos(rotX) * Math.Sin(rotY) * Math.Cos(rotZ) - Math.Sin(rotX) * Math.Sin(rotZ))),
        //    M32 = (float) (scale.Z * (Math.Cos(rotX) * Math.Sin(rotY) * Math.Sin(rotZ) - Math.Sin(rotX) * Math.Cos(rotZ))),
        //    M33 = (float) (scale.Z * (Math.Cos(rotX) * Math.Cos(rotY))),
        //    M34 = translation.Z,

        //    M41 = 0, M42 = 0, M43 = 0, M44 = 1
        //};
    }

    public static Matrix3X4<float> CreateLookAtMatrix(
        in Vector3 position,
        in Vector3 target,
        in Vector3 up
    )
    {
        Vector3 zaxis = Vector3.Normalize(position - target);
        Vector3 xaxis = Vector3.Normalize(Vector3.Cross(up, zaxis));
        Vector3 yaxis = Vector3.Normalize(Vector3.Cross(zaxis, xaxis));

        return new Matrix3X4<float>()
        {
            Row1 = new(xaxis.X, xaxis.Y, xaxis.Z, -Vector3.Dot(position, xaxis)),
            Row2 = new(yaxis.X, yaxis.Y, yaxis.Z, -Vector3.Dot(position, yaxis)),
            Row3 = new(zaxis.X, zaxis.Y, zaxis.Z, -Vector3.Dot(position, zaxis))
        };
    }

    // From Unity:
    public static Vector3 MultiplyQuaternionAndVector3(Quaternion rotation, Vector3 point)
    {
        float num1 = rotation.X * 2f;
        float num2 = rotation.Y * 2f;
        float num3 = rotation.Z * 2f;
        float num4 = rotation.X * num1;
        float num5 = rotation.Y * num2;
        float num6 = rotation.Z * num3;
        float num7 = rotation.X * num2;
        float num8 = rotation.X * num3;
        float num9 = rotation.Y * num3;
        float num10 = rotation.W * num1;
        float num11 = rotation.W * num2;
        float num12 = rotation.W * num3;

        return new()
        {
            X = (float)(
                (1.0 - ((double)num5 + (double)num6)) * (double)point.X
                + ((double)num7 - (double)num12) * (double)point.Y
                + ((double)num8 + (double)num11) * (double)point.Z
            ),
            Y = (float)(
                ((double)num7 + (double)num12) * (double)point.X
                + (1.0 - ((double)num4 + (double)num6)) * (double)point.Y
                + ((double)num9 - (double)num10) * (double)point.Z
            ),
            Z = (float)(
                ((double)num8 - (double)num11) * (double)point.X
                + ((double)num9 + (double)num10) * (double)point.Y
                + (1.0 - ((double)num4 + (double)num5)) * (double)point.Z
            )
        };
    }

    public static Matrix2X4<float> GetTextureTransform(
        Vector2 scale,
        float rotation,
        Vector2 translation,
        H3DTextureTransformType type
    )
    {
        Matrix2X4<float> matrix = new();

        float rotCos = (float)Math.Cos(rotation);
        float rotSin = (float)Math.Sin(rotation);

        matrix.M11 = scale.X * rotCos;
        matrix.M21 = scale.Y * rotSin;
        matrix.M12 = scale.X * -rotSin;
        matrix.M22 = scale.Y * rotCos;

        switch (type)
        {
            case H3DTextureTransformType.DccMaya:
                matrix.M14 = scale.X * (0.5f * rotSin - 0.5f * rotCos + 0.5f - translation.X);
                matrix.M24 = scale.Y * (0.5f * -rotSin - 0.5f * rotCos + 0.5f - translation.Y);
                break;

            case H3DTextureTransformType.DccSoftImage:
                matrix.M14 = scale.X * (-rotCos * translation.X - rotSin * translation.Y);
                matrix.M24 = scale.Y * (rotSin * translation.X - rotCos * translation.Y);
                break;

            case H3DTextureTransformType.Dcc3dsMax:
                matrix.M14 =
                    scale.X * rotCos * (-translation.X - 0.5f)
                    - scale.X * rotSin * (translation.Y - 0.5f)
                    + 0.5f;
                matrix.M24 =
                    scale.Y * rotSin * (-translation.X - 0.5f)
                    + scale.Y * rotCos * (translation.Y - 0.5f)
                    + 0.5f;
                break;
        }

        return matrix;
    }

    public static void ClearScale(ref Matrix4x4 matrix)
    {
        Vector3 row1 = Vector3.Normalize(new(matrix.M11, matrix.M12, matrix.M13));
        Vector3 row2 = Vector3.Normalize(new(matrix.M21, matrix.M22, matrix.M23));
        Vector3 row3 = Vector3.Normalize(new(matrix.M31, matrix.M32, matrix.M33));

        matrix.M11 = row1.X;
        matrix.M12 = row1.Y;
        matrix.M13 = row1.Z;

        matrix.M21 = row2.X;
        matrix.M22 = row2.Y;
        matrix.M23 = row2.Z;

        matrix.M31 = row3.X;
        matrix.M32 = row3.Y;
        matrix.M33 = row3.Z;
    }

    public static Matrix4X3<float> ToSilkNetMtx(this SPICA.Math3D.Matrix3x4 spicaMtx) =>
        new(
            spicaMtx.M11,
            spicaMtx.M12,
            spicaMtx.M13,
            spicaMtx.M21,
            spicaMtx.M22,
            spicaMtx.M23,
            spicaMtx.M31,
            spicaMtx.M32,
            spicaMtx.M33,
            spicaMtx.M41,
            spicaMtx.M42,
            spicaMtx.M43
        );
}
