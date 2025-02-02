using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.Maths;
using SPICA.Formats.CtrH3D.Model.Material;

namespace Autumn.Utils;

internal static class MathUtils
{
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

    public static Matrix4x4 CreateTransform(Vector3 translation, Vector3 scale, Vector3 rotation)
    {
        float rotX = (float)(Math.PI / 180 * rotation.X),
            rotY = (float)(Math.PI / 180 * rotation.Y),
            rotZ = (float)(Math.PI / 180 * rotation.Z);

        // M = S * (Rx * Ry * Rz) * T
        // Where T -> Translation, S -> Scale, R -> Rotation.

        Matrix4x4 mTranslation = Matrix4x4.CreateTranslation(translation);
        Matrix4x4 mScale = Matrix4x4.CreateScale(scale);

        Matrix4x4 mRotationX = Matrix4x4.CreateRotationX(rotX);
        Matrix4x4 mRotationY = Matrix4x4.CreateRotationY(rotY);
        Matrix4x4 mRotationZ = Matrix4x4.CreateRotationZ(rotZ);

        return mScale * (mRotationX * mRotationY * mRotationZ) * mTranslation;
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
                    scale.X * rotCos * (-translation.X - 0.5f) - scale.X * rotSin * (translation.Y - 0.5f) + 0.5f;
                matrix.M24 =
                    scale.Y * rotSin * (-translation.X - 0.5f) + scale.Y * rotCos * (translation.Y - 0.5f) + 0.5f;
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

    // From https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/GizmoDrawer.cs
    public static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float AP_x = p.X - a.X;
        float AP_y = p.Y - a.Y;

        float CP_x = p.X - b.X;
        float CP_y = p.Y - b.Y;

        bool s_ab = (b.X - a.X) * AP_y - (b.Y - a.Y) * AP_x > 0.0;

        if ( /*s_ac*/
            (c.X - a.X) * AP_y - (c.Y - a.Y) * AP_x > 0.0
            == s_ab
        )
            return false;

        if ( /*s_cb*/
            (c.X - b.X) * CP_y - (c.Y - b.Y) * CP_x > 0.0
            != s_ab
        )
            return false;

        return true;
    }

    // From https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/GizmoDrawer.cs
    public static bool IsPointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float AP_x = p.X - a.X;
        float AP_y = p.Y - a.Y;

        float CP_x = p.X - c.X;
        float CP_y = p.Y - c.Y;

        bool s_ab = (b.X - a.X) * AP_y - (b.Y - a.Y) * AP_x > 0.0;

        if ( /*s_ad*/
            (d.X - a.X) * AP_y - (d.Y - a.Y) * AP_x > 0.0
            == s_ab
        )
            return false;

        if ( /*s_cb*/
            (b.X - c.X) * CP_y - (b.Y - c.Y) * CP_x > 0.0
            == s_ab
        )
            return false;

        if ( /*s_cd*/
            (d.X - c.X) * CP_y - (d.Y - c.Y) * CP_x > 0.0
            != s_ab
        )
            return false;

        return true;
    }

    // From https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/GizmoDrawer.cs
    public static Vector3 IntersectPoint(Vector3 rayVector, Vector3 rayPoint, Vector3 planeNormal, Vector3 planePoint)
    {
        //code from: https://rosettacode.org/wiki/Find_the_intersection_of_a_line_with_a_plane
        var diff = rayPoint - planePoint;
        var prod1 = Vector3.Dot(diff, planeNormal);
        var prod2 = Vector3.Dot(rayVector, planeNormal);
        var prod3 = prod1 / prod2;
        return rayPoint - rayVector * prod3;
    }

    public static Vector3 Floor(Vector3 a)
    {
        a.X = (float)Math.Floor(a.X);
        a.Y = (float)Math.Floor(a.Y);
        a.Z = (float)Math.Floor(a.Z);
        return a;
    }

    public static Vector3 Round(Vector3 a)
    {
        a.X = (float)Math.Round(a.X);
        a.Y = (float)Math.Round(a.Y);
        a.Z = (float)Math.Round(a.Z);
        return a;
    }
}
