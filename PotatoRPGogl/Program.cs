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
using System.Diagnostics;

namespace PotatoRPGogl
{
    using Vec3 = Silk.NET.Maths.Vector3D;

    using Vec3f = Vector3D<float>;
    using Vec2f = Vector2D<float>;
    using Vec4f = Vector4D<float>;
    using Quat = Quaternion<float>;

    using Mat4f = Silk.NET.Maths.Matrix4X4<float>;

    class Program
    {
        const float Deg2Rad = (float)Math.PI / 180.0f;


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
        in vec4 vBoneIndices;  
        in vec4 vBoneWeights;

        out vec3 pass_norm;
        out vec2 pass_texCoords;
        out vec3 frag_pos;
        out vec3 col_debug;

        uniform mat4 bones[53];

        uniform mat4 viewProj;
        uniform mat4 model;
        
        void main()
        {
            mat4 boneTransform = mat4(0.0);

            boneTransform  +=    bones[int(vBoneIndices.x)] * vBoneWeights.x;
		    boneTransform  +=    bones[int(vBoneIndices.y)] * vBoneWeights.y;
		    boneTransform  +=    bones[int(vBoneIndices.z)] * vBoneWeights.z;
		    boneTransform  +=    bones[int(vBoneIndices.w)] * vBoneWeights.w;

            vec4 pos =  boneTransform * vec4(vPos.xyz, 1.0);

            col_debug = normalize(pos.xyz);

            gl_Position = (viewProj * model * vec4(vPos.xyz,1.0));
            frag_pos = (model *  vec4(vPos.xyz,1.0)).xyz;
            pass_norm = normalize((boneTransform * vec4(vNorm, 1.0)).xyz);//vNorm.xyz;
            pass_texCoords = vTexCoords;
        }
        ";

        static string FragmentShaderSource = @"
        #version 330 core
        in vec3 pass_norm;
        in vec2 pass_texCoords;
        in vec3 frag_pos;
        in vec3 col_debug;

        out vec4 FragColor;
        uniform sampler2D diffuseMap;

        uniform vec3 lightPos;
        uniform vec3 ambientCol;
        uniform vec3 lightCol;
        uniform vec3 viewPos;

        void main()
        {
            vec4 texColor = texture(diffuseMap, pass_texCoords);
            vec3 norm = normalize(pass_norm);
            vec3 light_dir = normalize(lightPos - frag_pos);
            float att = max(dot(norm, light_dir), 0.0);

            vec3 diffuse = lightCol * att;

            float specularStrength = 0.5;
            vec3 viewDir = normalize(viewPos - frag_pos);
            vec3 reflectDir = reflect(-light_dir, norm);
            float spec = pow(max(dot(viewDir, reflectDir), 0.0), 64);
            vec3 specular = specularStrength * spec * lightCol; 

            vec3 result = (ambientCol + diffuse + specular) *  texColor.xyz;
            FragColor = vec4(result, 1.0);
            //FragColor = vec4(col_debug, 1.0);
        }
        ";

        static Tuple<float[], int[]> vertexData;
        static Mat4f vpMatrix;
        static uint tex;
        static SkinnedModel skinnedModel;

        static void Main(string[] args)
        {
            AssimpContext importer = new AssimpContext();

            skinnedModel = new SkinnedModel(Gl, importer, "./Res/Models/heraklios.dae");
            vertexData = skinnedModel.getVertexData(); 
            vpMatrix = Mat4f.Identity;

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(800, 600);
            options.Title = "Potato RPG";

            window = Window.Create(options);
            window.Load += OnLoad;
            window.Update += OnUpdate;
            window.Render += OnRender;
            window.VSync = false;

            window.Run();
        }

        static unsafe void OnLoad()
        {
            IInputContext input = window.CreateInput();
            foreach (var keyboard in input.Keyboards)
            {
                keyboard.KeyDown += KeyDown;
                keyboard.KeyUp += KeyUp;
            }

            foreach (var mice in input.Mice)
            {
                mice.Cursor.CursorMode = CursorMode.Raw;
                mice.MouseMove += OnMouseMove;
            }

            Gl = GL.GetApi(window);
            Gl.Enable(GLEnum.CullFace);
            Gl.Disable(GLEnum.PolygonSmooth);
            Gl.Hint(GLEnum.PolygonSmoothHint, GLEnum.Fastest);
            Gl.CullFace(CullFaceMode.Back);
            Gl.Enable(GLEnum.Texture2D);
            Gl.Enable(GLEnum.DepthTest);
            Gl.DepthFunc(GLEnum.Lequal);

            tex = Utils.LoadImage(Gl, 0, "./Res/Models/heraklios_diff.png");

            Vao = Gl.GenVertexArray();
            Gl.BindVertexArray(Vao);

            Vbo = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
            fixed (void* v = &vertexData.Item1[0])
            {
                Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexData.Item1.Length * sizeof(float)), v, BufferUsageARB.StaticDraw); //Setting buffer data.
            }

            Ebo = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
            fixed (void* i = &vertexData.Item2[0])
            {
                Gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(vertexData.Item2.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw); //Setting buffer data.
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

            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 16 * sizeof(float), (void*)0); //pos
            Gl.EnableVertexAttribArray(0);

            Gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 16 * sizeof(float), (void*)(3 * sizeof(float))); //norms
            Gl.EnableVertexAttribArray(1);

            Gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 16 * sizeof(float), (void*)(6 * sizeof(float))); //uv
            Gl.EnableVertexAttribArray(2);

            Gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, 16 * sizeof(float), (void*)(8 * sizeof(float))); //bone indices
            Gl.EnableVertexAttribArray(3);

            Gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, 16 * sizeof(float), (void*)(12 * sizeof(float))); //bone weights
            Gl.EnableVertexAttribArray(4);
        }

        static float t = 0;
        static Matrix4X4<float> model;

        static Vec3f ambientColor = new Vec3f(93.0f / 255.0f, 140.0f / 255.0f, 174.0f / 255.0f);
        static Vec3f camPos = new Vec3f(0, 1.2f, -5);
        static Vec3f camFwd, camRight, camUp;

        static float deltaTime;
        static DateTime lastTime;
        static Quaternion<float> camRot = Quaternion<float>.Identity;

        static unsafe void OnRender(double obj)
        {
            var dt = DateTime.Now - lastTime;
            lastTime = DateTime.Now;
            deltaTime = (float)dt.TotalSeconds;

            Gl.ClearColor(ambientColor.X, ambientColor.Y, ambientColor.Z, 1);
            Gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

            Gl.BindVertexArray(Vao);
            Gl.UseProgram(shaderId);

            t += 180.0f * deltaTime;

            if (t > 360.0f)
                t = 0;

            var camFrontAssimp = Quaternion.Rotate(new Assimp.Vector3D(0, 0, 1), new Quaternion(camRot.W, camRot.X, camRot.Y, camRot.Z));
            camFwd = Utils.FromAssimp(camFrontAssimp);

            var camRightAssimp = Quaternion.Rotate(new Assimp.Vector3D(1, 0, 0), new Quaternion(camRot.W, camRot.X, camRot.Y, camRot.Z));
            camRight = Utils.FromAssimp(camRightAssimp);

            var camUpAssimp = Quaternion.Rotate(new Assimp.Vector3D(0, 1, 0), new Quaternion(camRot.W, camRot.X, camRot.Y, camRot.Z));
            camUp = Utils.FromAssimp(camUpAssimp);

            var view = Matrix4X4.CreateLookAt(camPos, camPos + camFwd, camUp);
            var projection = Matrix4X4.CreatePerspectiveFieldOfView<float>(60.0f * Deg2Rad, 800 / 600, 0.01f, 10000.0f);

            model = Matrix4X4<float>.Identity;
            model = Matrix4X4.Multiply(model, Matrix4X4.CreateScale( 0.025f)) ;
            //model = Matrix4X4.Multiply(model, Matrix4X4.CreateRotationY(t * Deg2Rad));

            vpMatrix = Mat4f.Identity;
            vpMatrix = Matrix4X4.Multiply(vpMatrix, view);
            vpMatrix = Matrix4X4.Multiply(vpMatrix, projection);

            Utils.SetUniform(Gl, shaderId, "model", model);
            Utils.SetUniform(Gl, shaderId, "viewProj", vpMatrix);

            Utils.SetUniform(Gl, shaderId, "diffuseMap", 0);
            Utils.SetUniform(Gl, shaderId, "lightCol", new Vec3f(1, 1, 0));
            Utils.SetUniform(Gl, shaderId, "lightPos", new Vec3f(20f, 5, 5));
            Utils.SetUniform(Gl, shaderId, "viewPos", camPos);
            Utils.SetUniform(Gl, shaderId, "ambientCol", ambientColor);

            foreach (var mesh in skinnedModel.skinnedMeshes)
            {
                Gl.ActiveTexture(GLEnum.Texture0);
                Gl.BindTexture(TextureTarget.Texture2D, tex);

                var currentPose = new Mat4f[53];
                for (int i = 0; i < 53; i++)
                    currentPose[i] = Mat4f.Identity;

                var elapsedTime = 1;// (float)DateTime.Now.Ticks / 1000;

                skinnedModel.getPose(ref currentPose, mesh.skeleton, elapsedTime);

                Utils.SetUniform(Gl, shaderId, "bones", currentPose.ToArray());

                Gl.DrawElementsBaseVertex(GLEnum.Triangles, (uint)mesh.indices.Length, DrawElementsType.UnsignedInt,
                    (void*)(sizeof(uint) * mesh.baseIndex), mesh.baseVertex);
            }
        }

        static DateTime lastFPSReadout;

        static void OnUpdate(double obj)
        {
            float camSpeed = 5f * deltaTime;
            camPos += camFwd * inputAxes.Y * camSpeed + camRight * inputAxes.X * camSpeed + camUp * inputAxes.Z * camSpeed;

            var euler = Quaternion<float>.CreateFromYawPitchRoll(0, 0, -45.0f * Deg2Rad * inputAxes.W * deltaTime);
            camRot = Quaternion<float>.Multiply(camRot, euler);

            if ((DateTime.Now - lastFPSReadout).TotalSeconds > 0.3f)
            {
                lastFPSReadout = DateTime.Now;
                window.Title = $"PotatoRPG FPS: {1.0f / deltaTime}";
            }
        }

        static void OnClose()
        {
            Gl.DeleteBuffer(Vbo);
            Gl.DeleteBuffer(Ebo);
            Gl.DeleteVertexArray(Vao);
            Gl.DeleteProgram(shaderId);

            Gl.DeleteTexture(tex);
        }

        static Vec4f inputAxes = new Vec4f(0, 0, 0, 0);

        private static void KeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            if (arg2 == Key.Escape)
            {
                window.Close();
            }

            if (arg2 == Key.W)
                inputAxes.Y = 1;
            if (arg2 == Key.S)
                inputAxes.Y = -1;

            if (arg2 == Key.A)
                inputAxes.X = 1;
            if (arg2 == Key.D)
                inputAxes.X = -1;

            if (arg2 == Key.ShiftLeft)
                inputAxes.Z = -1;
            if (arg2 == Key.Space)
                inputAxes.Z = 1;

            if (arg2 == Key.Q)
                inputAxes.W = -1;
            if (arg2 == Key.E)
                inputAxes.W = 1;
        }

        private static void KeyUp(IKeyboard arg1, Key arg2, int arg3)
        {
            if (arg2 == Key.W)
                inputAxes.Y = 0;
            if (arg2 == Key.S)
                inputAxes.Y = 0;
            if (arg2 == Key.A)
                inputAxes.X = 0;
            if (arg2 == Key.D)
                inputAxes.X = 0;
            if (arg2 == Key.Space)
                inputAxes.Z = 0;
            if (arg2 == Key.ShiftLeft)
                inputAxes.Z = 0;
            if (arg2 == Key.Q)
                inputAxes.W = 0;
            if (arg2 == Key.E)
                inputAxes.W = 0;
        }

        static System.Numerics.Vector2 lastMousePos;

        private static unsafe void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
        {
            var lookSensitivity = 0.1f;
            if (lastMousePos == default) { lastMousePos = position; }
            else
            {
                var xOffset = (position.X - lastMousePos.X) * lookSensitivity;
                var yOffset = (position.Y - lastMousePos.Y) * lookSensitivity;
                lastMousePos = position;

                var euler = Quaternion<float>.CreateFromYawPitchRoll(-xOffset * Deg2Rad, yOffset * Deg2Rad, 0);
                camRot = Quaternion<float>.Multiply(camRot, euler);
            }
        }
    }
}
