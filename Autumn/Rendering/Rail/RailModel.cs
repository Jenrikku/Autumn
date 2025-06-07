using System.Numerics;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Utils;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal class RailModel(RailObj rail)
{
    private const float _bezierPointStep = 0.2f;

    private bool _initialized = false;

    private uint _vertexBufferHandle;
    private uint _vertexArrayHandle;

    private Vector3[] _vertices = [];

    public void Initialize(GL gl)
    {
        if (_initialized)
            return;

        _vertexBufferHandle = gl.GenBuffer();
        _vertexArrayHandle = gl.GenVertexArray();
        _initialized = true;

        UpdateModel(gl);
    }

    public void UpdateModel(GL gl)
    {
        if (!_initialized)
            throw new InvalidOperationException(
                $@"{nameof(RailModel)} must be initialized before any calls to {nameof(UpdateModel)}"
            );

        List<Vector3> vertices = new();

        switch (rail.PointType)
        {
            case RailPointType.Bezier:

                for (int i = 0; i < rail.Points.Count - 1; i++)
                {
                    var point0 = (RailPointBezier)rail.Points[i];
                    var point1 = (RailPointBezier)rail.Points[i + 1];

                    var curve = CalcBezierCurveFrom(point0, point1, _bezierPointStep);
                    vertices.AddRange(curve);
                }

                if (rail.Closed)
                {
                    var point0 = (RailPointBezier)rail.Points[^1];
                    var point1 = (RailPointBezier)rail.Points[0];

                    var curve = CalcBezierCurveFrom(point0, point1, _bezierPointStep);
                    vertices.AddRange(curve);
                }

                break;

            case RailPointType.Linear:

                foreach (var point in rail.Points.Cast<RailPointLinear>())
                    vertices.Add(point.Translation * 0.01f);

                break;
        }

        _vertices = vertices.ToArray();

        gl.BindVertexArray(_vertexArrayHandle);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferHandle);

        gl.BufferData<Vector3>(BufferTargetARB.ArrayBuffer, _vertices, BufferUsageARB.StaticDraw);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

        gl.EnableVertexAttribArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }

    public void Draw(GL gl)
    {
        if (!_initialized)
            throw new InvalidOperationException(
                $@"{nameof(RailModel)} must be initialized before any calls to {nameof(Draw)}"
            );

        gl.BindVertexArray(_vertexArrayHandle);
        gl.DrawArrays(rail.Closed ? PrimitiveType.LineLoop : PrimitiveType.LineStrip, 0, (uint)_vertices.Length);
        gl.BindVertexArray(0);
    }

    private static IEnumerable<Vector3> CalcBezierCurveFrom(RailPointBezier point0, RailPointBezier point1, float step)
    {
        Vector3 p0 = point0.Point0Trans * 0.01f;
        Vector3 p1 = point0.Point2Trans * 0.01f;
        Vector3 p2 = point1.Point1Trans * 0.01f;
        Vector3 p3 = point1.Point0Trans * 0.01f;

        for (float t = 0; t <= 1; t += step)
        {
            Vector3 r = MathUtils.BezierPoint(p0, p1, p2, p3, t);
            yield return r;
        }
    }
}
