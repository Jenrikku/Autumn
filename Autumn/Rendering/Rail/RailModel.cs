using System.Numerics;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Utils;
using Silk.NET.OpenGL;

namespace Autumn.Rendering.Rail;

internal class RailModel
{
    private const float _bezierPointStep = 0.2f;

    private readonly RailObj _rail;

    private readonly uint _vertexBufferHandle;
    private readonly uint _vertexArrayHandle;

    private Vector3[] _vertices = [];

    public RailModel(GL gl, RailObj rail)
    {
        _rail = rail;
        _vertexBufferHandle = gl.GenBuffer();
        _vertexArrayHandle = gl.GenVertexArray();

        UpdateModel(gl);
    }

    public void UpdateModel(GL gl)
    {
        List<Vector3> vertices = new();

        switch (_rail.PointType)
        {
            case RailPointType.Bezier:

                for (int i = 0; i < _rail.Points.Count - 1; i++)
                {
                    var point0 = (RailPointBezier)_rail.Points[i];
                    var point1 = (RailPointBezier)_rail.Points[i + 1];

                    var curve = CalcBezierCurveFrom(point0, point1, _bezierPointStep);
                    vertices.AddRange(curve);
                }

                if (_rail.Closed)
                {
                    var point0 = (RailPointBezier)_rail.Points[^1];
                    var point1 = (RailPointBezier)_rail.Points[0];

                    var curve = CalcBezierCurveFrom(point0, point1, _bezierPointStep);
                    vertices.AddRange(curve);
                }

                break;

            case RailPointType.Linear:

                foreach (var point in _rail.Points.Cast<RailPointLinear>())
                    vertices.Add(point.Translation);

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
        gl.BindVertexArray(_vertexArrayHandle);
        gl.DrawArrays(_rail.Closed ? PrimitiveType.LineLoop : PrimitiveType.LineStrip, 0, (uint)_vertices.Length);
        gl.BindVertexArray(0);
    }

    private static IEnumerable<Vector3> CalcBezierCurveFrom(RailPointBezier point0, RailPointBezier point1, float step)
    {
        Vector3 p0 = point0.Point1Trans;
        Vector3 p1 = point0.Point2Trans;
        Vector3 p2 = point1.Point0Trans;
        Vector3 p3 = point1.Point2Trans;

        for (float t = 0; t < 1; t += step)
        {
            Vector3 r = MathUtils.BezierPoint(p0, p1, p2, p3, t);
            yield return r;
        }
    }
}
