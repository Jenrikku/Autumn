using System.Numerics;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Utils;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal class RailModel(RailObj rail)
{
    public Vector3 Offset = Vector3.Zero;
    private const float _bezierPointStep = 0.08f;

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

        List<Vector3> vertices = new();

        switch (rail.PointType)
        {
            case RailPointType.Bezier:

                for (int i = 0; i < rail.Points.Count - 1; i++)
                {
                    var point0 = rail.Points[i];
                    var point1 = rail.Points[i + 1];

                    var curve = CalcBezierCurveFrom(point0, point1, _bezierPointStep);
                    vertices.AddRange(curve);
                }

                if (rail.Closed)
                {
                    var point0 = rail.Points[^1];
                    var point1 = rail.Points[0];

                    var curve = CalcBezierCurveFrom(point0, point1, _bezierPointStep);
                    vertices.AddRange(curve);
                }

                break;

            case RailPointType.Linear:

                foreach (var point in rail.Points)
                    vertices.Add((Offset + point.Point0Trans) * 0.01f);

                break;
        }

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
                $@"{nameof(RailModel)} must be initialized before any calls to {nameof(Draw)}"
            );

        gl.BindVertexArray(_vertexArrayHandle);
        gl.DrawArrays(rail.Closed ? PrimitiveType.LineLoop : PrimitiveType.LineStrip, 0, (uint)_vertices.Length);
        gl.BindVertexArray(0);
    }

    private IEnumerable<Vector3> CalcBezierCurveFrom(RailPoint point0, RailPoint point1, float step)
    {
        Vector3 p0 = (Offset + point0.Point0Trans) * 0.01f;
        Vector3 p1 = (Offset + point0.Point2Trans) * 0.01f;
        Vector3 p2 = (Offset + point1.Point1Trans) * 0.01f;
        Vector3 p3 = (Offset + point1.Point0Trans) * 0.01f;

        for (float t = 0; t <= 1; t += step)
        {
            Vector3 r = MathUtils.BezierPoint(p0, p1, p2, p3, t);
            yield return r;
        }
        yield return p3;
    }
}
