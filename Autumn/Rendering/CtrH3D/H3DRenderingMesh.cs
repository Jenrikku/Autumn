using System.Diagnostics;
using Silk.NET.OpenGL;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.PICA.Commands;

namespace Autumn.Rendering.CtrH3D;

internal class H3DRenderingMesh : IDisposable
{
    private readonly GL _gl;

    private readonly uint _vertexBufferHandle;
    private readonly uint _vertexArrayHandle;

    // Indices for all submeshes.
    private readonly ushort[][] _subMeshesIndices;

    private bool _disposed = false;

    public unsafe H3DRenderingMesh(GL gl, H3DMesh mesh, H3DSubMeshCulling? subMeshCulling)
    {
        if (mesh.VertexStride <= 0)
            throw new ArgumentException("The mesh has an invalid vertex stride.");

        // Get indices:
        if (mesh.SubMeshes.Count == 0 && subMeshCulling.HasValue)
        {
            // Get indices by submesh culling
            _subMeshesIndices = new ushort[subMeshCulling.Value.SubMeshes.Count][];

            for (int i = 0; i < subMeshCulling.Value.SubMeshes.Count; i++)
                _subMeshesIndices[i] = subMeshCulling.Value.SubMeshes[i].Indices;
        }
        else
        {
            // Get indices by submeshes
            _subMeshesIndices = new ushort[mesh.SubMeshes.Count][];

            for (int i = 0; i < mesh.SubMeshes.Count; i++)
                _subMeshesIndices[i] = mesh.SubMeshes[i].Indices;
        }

        _gl = gl;

        int vertexCount = mesh.RawBuffer.Length / mesh.VertexStride;
        int fixedAttributesOffset = mesh.RawBuffer.Length;

        byte[] buffer;

        using (MemoryStream stream = new())
        {
            BinaryWriter writer = new(stream);

            stream.Write(mesh.RawBuffer);

            foreach (PICAFixedAttribute attribute in mesh.FixedAttributes)
            {
                float x = attribute.Value.X,
                    y = attribute.Value.Y,
                    z = attribute.Value.Z,
                    w = attribute.Value.W;

                for (int i = 0; i < vertexCount; i++)
                {
                    writer.Write(x);
                    writer.Write(y);
                    writer.Write(z);
                    writer.Write(w);
                }
            }

            buffer = stream.ToArray();
        }

        Debug.Assert(
            buffer.Length == fixedAttributesOffset + mesh.FixedAttributes.Count * 16 * vertexCount
        );

        _vertexBufferHandle = gl.GenBuffer();
        _vertexArrayHandle = gl.GenVertexArray();

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferHandle);
        gl.BufferData<byte>(BufferTargetARB.ArrayBuffer, buffer, BufferUsageARB.StaticDraw);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        gl.BindVertexArray(_vertexArrayHandle);

        for (uint i = 0; i < 16; i++)
            gl.DisableVertexAttribArray(i);

        int offset = 0;
        uint stride = (uint)mesh.VertexStride;

        foreach (PICAAttribute attribute in mesh.Attributes)
        {
            uint index = (uint)attribute.Name;
            int size = attribute.Elements;

            VertexAttribPointerType type = attribute.Format switch
            {
                PICAAttributeFormat.Byte => VertexAttribPointerType.Byte,
                PICAAttributeFormat.Ubyte => VertexAttribPointerType.UnsignedByte,
                PICAAttributeFormat.Short => VertexAttribPointerType.Short,
                PICAAttributeFormat.Float => VertexAttribPointerType.Float,
                _ => throw new("Unknown attribute format.")
            };

            switch (attribute.Format)
            {
                case PICAAttributeFormat.Short:
                    size <<= 1;
                    offset += offset & 1;
                    break;

                case PICAAttributeFormat.Float:
                    size <<= 2;
                    offset += offset & 1;
                    break;
            }

            gl.EnableVertexAttribArray(index);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferHandle);
            gl.VertexAttribPointer(index, size, type, false, stride, (void*)offset);

            offset += size;
        }

        foreach (PICAFixedAttribute attribute in mesh.FixedAttributes)
        {
            uint index = (uint)attribute.Name;

            gl.EnableVertexAttribArray(index);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferHandle);

            gl.VertexAttribPointer(
                index,
                sizeof(float),
                VertexAttribPointerType.Float,
                false,
                0,
                (void*)fixedAttributesOffset
            );

            fixedAttributesOffset += 0x10 * vertexCount;
        }

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    public void Draw()
    {
        _gl.BindVertexArray(_vertexArrayHandle);

        foreach (ushort[] indices in _subMeshesIndices)
        {
            _gl.DrawElements<ushort>(
                PrimitiveType.Triangles,
                (uint)indices.Length,
                DrawElementsType.UnsignedShort,
                indices
            );
        }

        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _gl.DeleteBuffer(_vertexBufferHandle);
        _gl.DeleteVertexArray(_vertexArrayHandle);

        _disposed = true;
    }
}
