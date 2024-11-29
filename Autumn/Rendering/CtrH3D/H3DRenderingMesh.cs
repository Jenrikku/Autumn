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
    private readonly uint _elementBufferHandle;
    private readonly (uint Start, uint Count)[] _elementInfo; // One per submesh

    private bool _disposed = false;

    public unsafe H3DRenderingMesh(GL gl, H3DMesh mesh, H3DSubMeshCulling? subMeshCulling)
    {
        if (mesh.VertexStride <= 0)
            throw new ArgumentException("The mesh has an invalid vertex stride.");

        _gl = gl;

        int vertexCount = mesh.RawBuffer.Length / mesh.VertexStride;
        int fixedAttributesOffset = mesh.RawBuffer.Length;

        byte[] vertexBuffer;

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

            vertexBuffer = stream.ToArray();
        }

        Debug.Assert(
            vertexBuffer.Length
                == fixedAttributesOffset + mesh.FixedAttributes.Count * 16 * vertexCount
        );

        _vertexBufferHandle = gl.GenBuffer();
        _elementBufferHandle = gl.GenBuffer();
        _vertexArrayHandle = gl.GenVertexArray();

        gl.BindVertexArray(_vertexArrayHandle);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferHandle);
        gl.BufferData<byte>(BufferTargetARB.ArrayBuffer, vertexBuffer, BufferUsageARB.StaticDraw);

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
            gl.VertexAttribPointer(index, attribute.Elements, type, false, stride, (void*)offset);

            offset += size;
        }

        foreach (PICAFixedAttribute attribute in mesh.FixedAttributes)
        {
            uint index = (uint)attribute.Name;

            gl.EnableVertexAttribArray(index);
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

        // Get indices:
        ushort[][] allIndices;
        int totalIndexAmount = 0;

        if (mesh.SubMeshes.Count == 0 && subMeshCulling.HasValue)
        {
            // Get indices from submesh culling
            int count = subMeshCulling.Value.SubMeshes.Count;
            allIndices = new ushort[count][];

            for (int i = 0; i < count; i++)
            {
                allIndices[i] = subMeshCulling.Value.SubMeshes[i].Indices;
                totalIndexAmount += allIndices[i].Length;
            }
        }
        else
        {
            // Get indices from submeshes
            uint count = (uint)mesh.SubMeshes.Count;
            allIndices = new ushort[count][];

            for (int i = 0; i < count; i++)
            {
                allIndices[i] = mesh.SubMeshes[i].Indices;
                totalIndexAmount += allIndices[i].Length;
            }
        }

        _elementInfo = new (uint, uint)[allIndices.Length];

        ushort[] indexBuffer = new ushort[totalIndexAmount];
        uint lastIndexOffset = 0;

        for (int i = 0; i < allIndices.Length; i++)
        {
            ushort[] indices = allIndices[i];

            _elementInfo[i] = (lastIndexOffset * sizeof(ushort), (uint)indices.Length);

            Array.Copy(indices, 0, indexBuffer, lastIndexOffset, indices.Length);
            lastIndexOffset += (uint)indices.Length;
        }

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _elementBufferHandle);
        gl.BufferData<ushort>(
            BufferTargetARB.ElementArrayBuffer,
            indexBuffer,
            BufferUsageARB.StaticDraw
        );

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
    }

    public unsafe void Draw()
    {
        if (_disposed)
            return;

        _gl.BindVertexArray(_vertexArrayHandle);

        foreach (var (start, lenght) in _elementInfo)
        {
            _gl.DrawElements(
                PrimitiveType.Triangles,
                lenght,
                DrawElementsType.UnsignedShort,
                (void*)start
            );
        }

        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _gl.DeleteBuffer(_vertexBufferHandle);
        _gl.DeleteBuffer(_elementBufferHandle);
        _gl.DeleteVertexArray(_vertexArrayHandle);

        _disposed = true;
    }
}
