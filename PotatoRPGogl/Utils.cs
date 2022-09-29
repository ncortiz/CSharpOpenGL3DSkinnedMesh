using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Silk.NET.OpenGL;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PotatoRPGogl
{
    using Vec3f = Vector3D<float>;
    using Mat4f = Silk.NET.Maths.Matrix4X4<float>;

    class Utils
    {
        public static unsafe bool SetUniform<T>(GL gl, uint shader, string uniform, T v)
        {
            int location = gl.GetUniformLocation(shader, uniform);

            if (location == -1)
            {
                Console.WriteLine($"Cannot find uniform '{uniform}' of type '{typeof(T).Name}' for shader");
                return false;
            }

            if (typeof(T) == typeof(Mat4f))
            {
                var mat = (Mat4f)Convert.ChangeType(v, typeof(Mat4f));
                var matrix_values = new float[4 * 4]
                {
                    mat.M11,mat.M12,mat.M13,mat.M14,
                    mat.M21,mat.M22,mat.M23,mat.M24,
                    mat.M31,mat.M32,mat.M33,mat.M34,
                    mat.M41,mat.M42,mat.M43,mat.M44
                };

                fixed (float* i = &matrix_values[0])
                {
                    gl.UniformMatrix4(location, 1, false, i);
                }
            }
            else if (typeof(T) == typeof(Vec3f))
            {
                var vec3 = (Vec3f)Convert.ChangeType(v, typeof(Vec3f));
                gl.ProgramUniform3(shader, location, vec3.X, vec3.Y, vec3.Z);
            }
            else if (typeof(T) == typeof(float))
            {
                var f = (float)Convert.ChangeType(v, typeof(float));
                gl.ProgramUniform1(shader, location, f);
            }
            else if (typeof(T) == typeof(int))
            {
                var i = (int)Convert.ChangeType(v, typeof(int));
                gl.ProgramUniform1(shader, location, i);
            }
            else
                throw new Exception($"Attempted to set uniform '{uniform}' of type '{typeof(T).Name}' which is not implemented.");

            return true;
        }

        public static unsafe void SetUniform<T>(GL gl, uint shader, string uniform, T[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!SetUniform(gl, shader, uniform + $"[{i}]", values[i]))
                    return;
            }
        }

        public unsafe static uint LoadImage(GL gl, int textureSlot, string path)
        {
            if (textureSlot > 31)
                throw new Exception("Attempted to load image to slot greater than 31 (max slot)");

            uint tex = gl.GenTexture();
            gl.ActiveTexture(GLEnum.Texture0 + textureSlot);
            gl.BindTexture(TextureTarget.Texture2D, tex);

            using (var img = Image.Load<Rgba32>(path))
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        fixed (void* data = accessor.GetRowSpan(y))
                        {
                            gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                        }
                    }
                });
            }

            gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureBaseLevel, 0);
            gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMaxLevel, 8);
            gl.GenerateMipmap(TextureTarget.Texture2D);

            Console.WriteLine($"Loaded image ");

            return tex;
        }


        public static Vec3f FromAssimp(Assimp.Vector3D v)
            => new Vec3f(v.X, v.Y, v.Z);


        public static Mat4f FromAssimp(Assimp.Matrix4x4 assimpMat)
        {
            return new Mat4f(assimpMat.A1, assimpMat.A2, assimpMat.A3, assimpMat.A4,
                assimpMat.B1, assimpMat.B2, assimpMat.B3, assimpMat.B4,
                assimpMat.C1, assimpMat.C2, assimpMat.C3, assimpMat.C4,
                assimpMat.D1, assimpMat.D2, assimpMat.D3, assimpMat.D4);
        }

        public static Assimp.Matrix4x4 ToAssimp(Mat4f mat)
        {
            return new Assimp.Matrix4x4(mat.M11, mat.M12, mat.M13, mat.M14,
                mat.M21, mat.M22, mat.M23, mat.M24,
                mat.M31, mat.M32, mat.M33, mat.M34,
                mat.M41, mat.M42, mat.M43, mat.M44);
        }
    }
}
