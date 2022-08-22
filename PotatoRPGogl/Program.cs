using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

using Silk.NET.OpenGL;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Assimp;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace PotatoRPGogl
{
    using Vec3f = Vector3D<float>;
    using Vec2f = Vector2D<float>;
    using Vec4f = Vector4D<float>;
    using Mat4f = Silk.NET.Maths.Matrix4X4<float>;

    class Mesh
    {
        public struct VertexData
        {
            public Vec3f position;
            public Vec3f normal;
            public Vec2f texCoords;
            public Vec4f boneWeights;
            public Vec4f boneIDs;
        }

        public int[] indices;
        public VertexData[] vertices;
        public int baseVertex
            => vertices.Length;
        public int baseIndex
            => indices.Length;

        public Mesh(VertexData[] vertices, int[] indices)
        {
            this.vertices = vertices;
            this.indices = indices;
        }
    }

    class Program
    {
        const float PI2Rad = (float)Math.PI / 180.0f;

        static IWindow window;
        static GL Gl;

        static uint Vbo;
        static uint Ebo;
        static uint Vao;
        static uint shaderId;

        static string VertexShaderSource = @"
        #version 330 core
        in vec3 vPos;
        in vec3 vNorm;
        in vec2 vTexCoords;

        out vec3 pass_norm;
        out vec2 pass_texCoords;
        out vec3 frag_pos;

        uniform mat4 modelViewProj;
        uniform mat4 model;
        
        void main()
        {
            gl_Position = modelViewProj * vec4(vPos.x, vPos.y, vPos.z, 1.0);
            frag_pos = (model * vec4(vPos, 1.0)).xyz;
            pass_norm = vNorm;
            pass_texCoords = vTexCoords;
        }
        ";

        static string FragmentShaderSource = @"
        #version 330 core
        in vec3 pass_norm;
        in vec2 pass_texCoords;
        in vec3 frag_pos;
        out vec4 FragColor;
        uniform sampler2D diffuseMap;

        uniform vec3 lightPos;
        uniform vec3 viewPos;

        void main()
        {
            vec4 texColor = texture(diffuseMap, pass_texCoords);
            vec3 norm = normalize(pass_norm);
            vec3 light_dir = normalize(lightPos - frag_pos);
            float att = max(dot(norm, light_dir), 0.0);

            vec3 ambient = vec3(0.1f, 0.2f, 1.0f);
            ambient = ambient * 0.8f;

            vec3 lightColor = vec3(1.0f,0.8f,0.0f);
            vec3 diffuse = lightColor * att;

            float specularStrength = 0.5;
            vec3 viewDir = normalize(viewPos - frag_pos);
            vec3 reflectDir = reflect(-light_dir, norm);
            float spec = pow(max(dot(viewDir, reflectDir), 0.0), 64);
            vec3 specular = specularStrength * spec * lightColor; 

            //FragColor = vec4(color, 1.0f);//vec4(1.0f, 0.5f, 0.2f, 1.0f);

            vec3 result = (ambient + diffuse + specular) *  texColor.xyz;
            FragColor = vec4(result, 1.0);
        }
        ";

        static int[] index_data;
        static float[] vertex_data;
        static Mat4f mvpMatrix;
        static uint tex;
        static List<Mesh> meshes;

        static void Main(string[] args)
        {
            AssimpContext importer = new AssimpContext();

            meshes = LoadModel(importer, "./Res/Models/heraklios.dae");

            var vertices = new List<float>();
            var indices = new List<int>();

            foreach(var mesh in meshes)
            {
                foreach(var vertex in mesh.vertices)
                {
                    vertices.Add(vertex.position.X);
                    vertices.Add(vertex.position.Y);
                    vertices.Add(vertex.position.Z);

                    vertices.Add(vertex.normal.X);
                    vertices.Add(vertex.normal.Y);
                    vertices.Add(vertex.normal.Z);

                    vertices.Add(vertex.texCoords.X);
                    vertices.Add(vertex.texCoords.Y);
                }

                indices.AddRange(mesh.indices);
            }

            vertex_data = vertices.ToArray();
            index_data = indices.ToArray();

            mvpMatrix = Mat4f.Identity;

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(800, 600);
            options.Title = "Potato RPG";

            window = Window.Create(options);
            window.Load += OnLoad;
            window.Update += OnUpdate;
            window.Render += OnRender;

            window.Run();
        }

        static unsafe void OnLoad()
        {

            IInputContext input = window.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
            {
                input.Keyboards[i].KeyDown += KeyDown;
            }

            Gl = GL.GetApi(window);
            Gl.Enable(GLEnum.CullFace);
            Gl.Disable(GLEnum.PolygonSmooth);
            Gl.Hint(GLEnum.PolygonSmoothHint, GLEnum.Fastest);
            Gl.CullFace(CullFaceMode.Back);
            Gl.Enable(GLEnum.Texture2D);
            Gl.Enable(GLEnum.DepthTest);
            Gl.DepthFunc(GLEnum.Lequal);

            LoadImage("./Res/Models/heraklios_body_diff.png");

            Vao = Gl.GenVertexArray();
            Gl.BindVertexArray(Vao);

            Vbo = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
            fixed (void* v = &vertex_data[0])
            {
                Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertex_data.Length * sizeof(float)), v, BufferUsageARB.StaticDraw); //Setting buffer data.
            }

            Ebo = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
            fixed (void* i = &index_data[0])
            {
                Gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(index_data.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw); //Setting buffer data.
            }

            uint vertexShader = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(vertexShader, VertexShaderSource);
            Gl.CompileShader(vertexShader);

            string infoLog = Gl.GetShaderInfoLog(vertexShader);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                Console.WriteLine($"Error compiling vertex shader {infoLog}");
            }

            uint fragmentShader = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(fragmentShader, FragmentShaderSource);
            Gl.CompileShader(fragmentShader);

            infoLog = Gl.GetShaderInfoLog(fragmentShader);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                Console.WriteLine($"Error compiling fragment shader {infoLog}");
            }

            shaderId = Gl.CreateProgram();
            Gl.AttachShader(shaderId, vertexShader);
            Gl.AttachShader(shaderId, fragmentShader);
            Gl.LinkProgram(shaderId);

            Gl.GetProgram(shaderId, GLEnum.LinkStatus, out var status);
            if (status == 0)
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(shaderId)}");

            Gl.DetachShader(shaderId, vertexShader);
            Gl.DetachShader(shaderId, fragmentShader);
            Gl.DeleteShader(vertexShader);
            Gl.DeleteShader(fragmentShader);

            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)0); //pos
            Gl.EnableVertexAttribArray(0);

            Gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float))); //norms
            Gl.EnableVertexAttribArray(1);

            Gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*) (6 * sizeof(float))); //uv
            Gl.EnableVertexAttribArray(2);
        }

        static float t = 0;
        static Matrix4X4<float> model;

        static unsafe void OnRender(double obj)
        {
            Gl.ClearColor(0.1f, 0.2f, 1, 1);
            Gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

            Gl.BindVertexArray(Vao);
            Gl.UseProgram(shaderId);

            t += 1f;
            var camPos = new Vec3f(0, 1.2f, -5);

            var camFront = new Vec3f(0, 0, 1);
            var view = Matrix4X4.CreateLookAt(camPos, camPos + camFront, new Vec3f(0, 1, 0));
            var projection = Matrix4X4.CreatePerspectiveFieldOfView<float>(60.0f * PI2Rad, 800 / 600, 0.01f, 100.0f);

            model = Matrix4X4<float>.Identity;
            model = Matrix4X4.Multiply(model, Matrix4X4.CreateScale(0.02f));
            model = Matrix4X4.Multiply(Matrix4X4.CreateRotationY((t % 360) * PI2Rad), model);

            mvpMatrix = Mat4f.Identity;
            mvpMatrix = Matrix4X4.Multiply(mvpMatrix, model);
            mvpMatrix = Matrix4X4.Multiply(mvpMatrix, view);
            mvpMatrix = Matrix4X4.Multiply(mvpMatrix, projection);

            SetUniformMat4f(shaderId, "model", model);
            SetUniformMat4f(shaderId, "modelViewProj", mvpMatrix);

            SetUniform(shaderId, "diffuseMap", 0);
            SetUniformVec3(shaderId, "lightPos", new Vec3f(0.0f, 4.0f, 5.0f));
            SetUniformVec3(shaderId, "viewPos", camPos);

            Gl.ActiveTexture(GLEnum.Texture0);
            Gl.BindTexture(TextureTarget.Texture2D, tex);
 
            foreach(var mesh in meshes)
            {
                Gl.DrawElementsBaseVertex(GLEnum.Triangles, (uint)mesh.indices.Length, DrawElementsType.UnsignedInt,
                    (void*)(sizeof(uint) * mesh.baseIndex), mesh.baseVertex);
            }
        }

        static void OnUpdate(double obj)
        {

        }

        static void OnClose()
        {
            Gl.DeleteBuffer(Vbo);
            Gl.DeleteBuffer(Ebo);
            Gl.DeleteVertexArray(Vao);
            Gl.DeleteProgram(shaderId);

            Gl.DeleteTexture(tex);
        }

        private static void KeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            if (arg2 == Key.Escape)
            {
                window.Close();
            }
        }

        static List<Mesh> LoadModel(AssimpContext importer, string path)
        {
            Scene scene = importer.ImportFile(path);
            var allVertices = new List<Mesh>();

            //mats and texs

            foreach(var anim in scene.Animations)
            {
            }

            var bones = new List<Bone>();

            int uvsc = 0;
            foreach (var mesh in scene.Meshes)
            {
                var meshVerts = new List<Mesh.VertexData>();
                var meshIndices = new List<int>();

                var verts = mesh.Vertices;
                var norms = mesh.HasNormals ? mesh.Normals : null;

                var uvs = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0] : null;

                for (int i = 0; i < verts.Count; i++)
                {
                    var v = new Mesh.VertexData();
                    v.position = new Vec3f(verts[i].X, verts[i].Y, verts[i].Z);

                    if (norms != null)
                        v.normal = new Vec3f(norms[i].X, norms[i].Y, norms[i].Z);

                  //  v.boneWeights = mesh.

                    if (uvs != null)
                        v.texCoords = new Vec2f(uvs[i].X, uvs[i].Y);
                    else
                        uvsc++;

                    
                    meshVerts.Add(v);
                }

                foreach (var face in mesh.Faces)
                {
                    meshIndices.AddRange(face.Indices);
                }

                allVertices.Add(new Mesh(meshVerts.ToArray(), meshIndices.ToArray()));
            }

           /* foreach(var bone in bones)
            {
                 bone.VertexWeights[0].
            }*/

            Console.WriteLine($"[DEBUG] $ Loaded {scene.AnimationCount} animation(s)" +
                $"" +
                $" {scene.MeshCount} meshes ");

            return allVertices;
        }

        unsafe static void LoadImage(string path)
        {
            tex = Gl.GenTexture();
            Gl.ActiveTexture(GLEnum.Texture0);
            Gl.BindTexture(TextureTarget.Texture2D, tex);

            using (var img = Image.Load<Rgba32>(path))
            {
                Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        fixed (void* data = accessor.GetRowSpan(y))
                        {
                            Gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                        }
                    }
                });
            }

            Gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            Gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            Gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            Gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            Gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureBaseLevel, 0);
            Gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMaxLevel, 8);
            Gl.GenerateMipmap(TextureTarget.Texture2D);

            Console.WriteLine($"Loaded image ");
        }

        static unsafe void SetUniform(uint shader, string uniform, float v)
        {
            int location = Gl.GetUniformLocation(shaderId, uniform);
            if (location == -1)
            {
                Console.WriteLine($"Cannot find uniform '{uniform}' for shader");
                return;
            }
            Gl.ProgramUniform1(shaderId, location, v);
        }

        static unsafe void SetUniformVec3(uint shader, string uniform, Vec3f v)
        {
            int location = Gl.GetUniformLocation(shaderId, uniform);
            if (location == -1)
            {
                Console.WriteLine($"Cannot find uniform '{uniform}' for shader");
                return;
            }
            Gl.ProgramUniform3(shaderId, location, v.X, v.Y, v.Z);
        }
        
        static unsafe void SetUniformMat4f(uint shader, string uniform, Mat4f mat)
        {
            int location = Gl.GetUniformLocation(shaderId, uniform);

            if (location == -1)
            {
                Console.WriteLine($"Cannot find uniform '{uniform}' for shader");
                return;
            }

            var matrix_values = new float[4 * 4]
            {
                mat.M11,mat.M12,mat.M13,mat.M14,
                mat.M21,mat.M22,mat.M23,mat.M24,
                mat.M31,mat.M32,mat.M33,mat.M34,
                mat.M41,mat.M42,mat.M43,mat.M44
            };

            fixed (float* i = &matrix_values[0])
            {
                Gl.UniformMatrix4(location, 1, false, i);
            }
        }
    }
}
