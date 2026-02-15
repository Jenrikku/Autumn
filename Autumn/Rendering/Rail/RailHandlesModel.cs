using System.Numerics;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Utils;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal class RailHandlesModel(RailPoint point)
{

    public bool Initialized { get; private set; }

    private GL? _gl;

    private uint _vertexBufferHandle;
    private uint _vertexArrayHandle;

    private Vector3[] _vertices = [];
    public void Initialize(GL gl)
    {
        if (Initialized)
            return;

        _gl = gl;
        _vertexBufferHandle = gl.GenBuffer();
        _vertexArrayHandle = gl.GenVertexArray();
        Initialized = true;

        UpdateModel();
    }

    public void UpdateModel()
    {
        if (!Initialized)
            throw new InvalidOperationException(
                $@"{nameof(RailModel)} must be initialized before any calls to {nameof(UpdateModel)}"
            );

        List<Vector3> vertices = [point.Point1Trans * 0.01f, point.Point0Trans * 0.01f, point.Point2Trans * 0.01f];

        _vertices = vertices.ToArray();

        _gl!.BindVertexArray(_vertexArrayHandle);
        _gl!.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferHandle);

        _gl!.BufferData<Vector3>(BufferTargetARB.ArrayBuffer, _vertices, BufferUsageARB.StaticDraw);
        _gl!.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

        _gl!.EnableVertexAttribArray(0);
        _gl!.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl!.BindVertexArray(0);
    }

    public void Draw(GL gl)
    {
        if (!Initialized)
            throw new InvalidOperationException(
                $@"{nameof(RailHandlesModel)} must be initialized before any calls to {nameof(Draw)}"
            );

        gl.BindVertexArray(_vertexArrayHandle);
        gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)_vertices.Length);
        gl.BindVertexArray(0);
    }
}
