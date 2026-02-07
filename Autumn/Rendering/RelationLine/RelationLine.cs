using System.Numerics;
using Autumn.Enums;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Utils;
using SceneGL;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal static class RelationLine
{

    private static readonly Matrix4x4 s_lineTranslate = Matrix4x4.CreateTranslation(new(0.01f));
    public static bool Initialized { get; private set; }

    private static uint _vertexBufferHandle;
    private static uint _vertexArrayHandle;

    private static Vector3[] _vertices = [Vector3.One, Vector3.One];
    public static void Initialize(GL gl)
    {
        if (Initialized)
            return;

        _vertexBufferHandle = gl.GenBuffer();
        _vertexArrayHandle = gl.GenVertexArray();
        Initialized = true;
        UpdateModel(gl);
    }

    public static void UpdateModel(GL gl)
    {
        if (!Initialized)
            throw new InvalidOperationException(
                $@"{nameof(RelationLine)} must be initialized before any calls to {nameof(UpdateModel)}"
            );

        gl!.BindVertexArray(_vertexArrayHandle);
        gl!.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferHandle);

        gl!.BufferData<Vector3>(BufferTargetARB.ArrayBuffer, _vertices, BufferUsageARB.StaticDraw);
        gl!.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

        gl!.EnableVertexAttribArray(0);
        gl!.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl!.BindVertexArray(0);
    }

    public static void Draw(GL gl)
    {
        if (!Initialized)
            throw new InvalidOperationException(
                $@"{nameof(RelationLine)} must be initialized before any calls to {nameof(Draw)}"
            );

        gl.BindVertexArray(_vertexArrayHandle);
        gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)_vertices.Length);
        gl.BindVertexArray(0);
    }
    public static void Render(GL gl, CommonSceneParameters scene, RelationLineParams line, CommonMaterialParameters material, uint pickingId, Vector3 A, Vector3 B)
    {
        scene.Transform = s_lineTranslate;

        line.PosA = A;
        line.PosB = B;

        if (!RelationLineMaterial.TryUse(gl, scene, line, material, out ProgramUniformScope scope))
            return;

        using (scope)
        {
            gl.CullFace(TriangleFace.Back);
            
            if (RelationLineMaterial.Program.TryGetUniformLoc("uPickingId", out int location))
                gl.Uniform1(location, pickingId);
            Draw(gl);
        }
    }
}
