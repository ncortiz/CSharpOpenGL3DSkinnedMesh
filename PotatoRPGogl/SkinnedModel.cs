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

    public class BoneTransformTrack
    {
        public List<float> positionTimestamps = new List<float>();
        public List<float> rotationTimestamps = new List<float>();
        public List<float> scaleTimestamps = new List<float>();

        public List<Vec3f> positions = new List<Vec3f>();
        public List<Quat> rotations = new List<Quat>();
        public List<Vec3f> scales = new List<Vec3f>();
    }

    public class Animation
    {
        public float duration = 0.0f;
        public float ticksPerSec = 1;
        public Dictionary<string, BoneTransformTrack> boneTransforms = new Dictionary<string, BoneTransformTrack>();
    }

    public class VertexData
    {
        public Vec3f position;
        public Vec3f normal;
        public Vec2f texCoords;
        public Vec4f boneIDs;
        public Vec4f boneWeights;
    }

    public class BoneData
    {
        public int id = 0;
        public string name = "";
        public Mat4f offset = Mat4f.Identity;
        public Mat4f node = Mat4f.Identity;
        public int parentId;

        public List<BoneData> children = new List<BoneData>();
    }


    class SkinnedMesh
    {
        public BoneData[] skeleton;
        public int[] indices;
        public VertexData[] vertices;

        public int baseVertex
            => vertices.Length;
        public int baseIndex
            => indices.Length;
    }

    class SkinnedModel
    {
        GL Gl;

        public SkinnedModel(GL Gl, AssimpContext importer, string path)
        {
            this.Gl = Gl;
            LoadModel(importer, path);
        }

        void LoadAnims(Scene scene)
        {
            var anims = new List<Animation>();

            foreach (var anim in scene.Animations)
            {
                var animation = new Animation();

                if (anim.TicksPerSecond != 0.0f)
                {
                    animation.ticksPerSec = (float)anim.TicksPerSecond;
                }
                else
                    animation.ticksPerSec = 1.0f;

                animation.duration = (float)(anim.TicksPerSecond * anim.DurationInTicks);
                animation.boneTransforms = new Dictionary<string, BoneTransformTrack>();

                foreach (var channel in anim.NodeAnimationChannels)
                {
                    var track = new BoneTransformTrack();

                    foreach (var posKey in channel.PositionKeys)
                    {
                        track.positionTimestamps.Add((float)posKey.Time);
                        track.positions.Add(new Vec3f(posKey.Value.X, posKey.Value.Y, posKey.Value.Z));
                    }

                    foreach (var rotKey in channel.RotationKeys)
                    {
                        track.rotationTimestamps.Add((float)rotKey.Time);
                        track.rotations.Add(new Quat(rotKey.Value.X, rotKey.Value.Y, rotKey.Value.Z, rotKey.Value.W));
                    }

                    foreach (var scaleKey in channel.ScalingKeys)
                    {
                        track.scaleTimestamps.Add((float)scaleKey.Time);
                        track.scales.Add(new Vec3f(scaleKey.Value.X, scaleKey.Value.Y, scaleKey.Value.Z));
                    }

                    animation.boneTransforms.Add(channel.NodeName, track);
                }

                anims.Add(animation);
            }

            this.animations = anims.ToArray();
        }

        static Tuple<int, float> getTimeFraction(List<float> times, float dt)
        {
            int segment = 0;
            while (dt > times[segment])
                segment++;
            float start = times[segment - 1];
            float end = times[segment];
            float frac = (dt - start) / (end - start);
            return Tuple.Create(segment, frac);
        }

        public void getPose(ref Mat4f[] res, BoneData[] bones, float dt)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i].parentId != -1)
                { 
                    var global = Matrix4X4.Multiply(bones[bones[i].parentId].node, bones[i].node);
                    var b2 = Matrix4X4.Multiply(global, bones[i].offset);

                    res[i] = b2;// Matrix4X4.Multiply(globalInverseTransform, b2);
                
                }

                foreach(var child in bones[i].children)
                {

                }

            }
        }

        void GetNodeParents(Assimp.Node root, ref Dictionary<string, string> nodeParents)
        {
            foreach(var child in root.Children)
            {
                nodeParents.Add(child.Name, root.Name);
                GetNodeParents(child, ref nodeParents);
            }
        }

        void GetNodalMatrices(ref Mat4f[] nodes, Assimp.Node root, Dictionary<string, int> boneIds)
        {
            if (boneIds.ContainsKey(root.Name))
            {
                nodes[boneIds[root.Name]] = Utils.FromAssimp(root.Transform);
            }

            foreach (var child in root.Children)
                GetNodalMatrices(ref nodes, child, boneIds);
        }

        void LoadModel(AssimpContext importer, string path)
        {
            Scene scene = importer.ImportFile(path);
            var allMeshes = new List<SkinnedMesh>();

            LoadAnims(scene);

            foreach (var mesh in scene.Meshes)
            {
                var meshVerts = new VertexData[mesh.VertexCount];

                for (int i = 0; i < mesh.VertexCount; i++)
                    meshVerts[i] = new VertexData();

                var meshIndices = new List<int>();

                var verts = mesh.Vertices;
                var norms = mesh.HasNormals ? mesh.Normals : null;

                var uvs = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0] : null;

                for (int i = 0; i < verts.Count; i++)
                {
                    meshVerts[i].position = new Vec3f(verts[i].X, verts[i].Y, verts[i].Z);

                    if (norms != null)
                        meshVerts[i].normal = new Vec3f(norms[i].X, norms[i].Y, norms[i].Z);

                    if (uvs != null)
                        meshVerts[i].texCoords = new Vec2f(uvs[i].X, uvs[i].Y);
                }


                var bones = new List<BoneData>();
                var bone_ids = new Dictionary<string, int>();
                var bone_counts = new uint[mesh.VertexCount]; //for Vertex data XYZ swizzle

                for (int i = 0; i < mesh.BoneCount; i++)
                {
                    var bone = mesh.Bones[i];

                    var mat = bone.OffsetMatrix;
                    // mat.Transpose();
                    
                    var boneData = new BoneData();
                    boneData.name = bone.Name;
                    boneData.offset = Utils.FromAssimp(bone.OffsetMatrix);
                    boneData.id = i;
                    bones.Add(boneData);
                    bone_ids.Add(bone.Name, i);

                    foreach (var vertex in bone.VertexWeights)
                    {
                        var id = i;
                        var wt = vertex.Weight;

                        bone_counts[vertex.VertexID]++;

                        switch (bone_counts[vertex.VertexID])
                        {
                            case 1:
                                meshVerts[vertex.VertexID].boneIDs.X = id;
                                meshVerts[vertex.VertexID].boneWeights.X = wt;
                                break;
                            case 2:
                                meshVerts[vertex.VertexID].boneIDs.Y = id;
                                meshVerts[vertex.VertexID].boneWeights.Y = wt;
                                break;
                            case 3:
                                meshVerts[vertex.VertexID].boneIDs.Z = id;
                                meshVerts[vertex.VertexID].boneWeights.Z = wt;
                                break;
                            case 4:
                                meshVerts[vertex.VertexID].boneIDs.W = id;
                                meshVerts[vertex.VertexID].boneWeights.W = wt;
                                break;
                            default:
                                Console.WriteLine("[WARNING] Unable to allocate more bone data entries for mesh " + mesh.Name);
                                break;
                        }
                    }
                }

                //Normalize bone weights
                for (int i = 0; i < meshVerts.Length; i++)
                {
                    var weights = meshVerts[i].boneWeights;
                    float totalWeight = weights.X + weights.Y + weights.Z + weights.W;

                    if (totalWeight > 0.0f)
                    {
                        meshVerts[i].boneWeights = new Vec4f(
                            weights.X / totalWeight,
                            weights.Y / totalWeight,
                            weights.Z / totalWeight,
                            weights.W / totalWeight
                        );
                    }
                }
                mesh.Faces.ForEach(f => meshIndices.AddRange(f.Indices));

                var m = new SkinnedMesh();
                m.vertices = meshVerts.ToArray();
                m.indices = meshIndices.ToArray();

                var nodes = new Mat4f[53];
                GetNodalMatrices(ref nodes, scene.RootNode, bone_ids);

                var nodeParents = new Dictionary<string, string>();
                GetNodeParents(scene.RootNode, ref nodeParents);

                for (int i = 0; i < bones.Count; i++)
                {
                    if (!nodeParents.ContainsKey(bones[i].name))
                        bones[i].parentId = -1;
                    else
                        bones[i].parentId = bones.FindIndex(x => x.name.Equals(nodeParents[bones[i].name]));

                    bones[i].node = nodes[i];
                }

                m.skeleton = bones.ToArray();



                allMeshes.Add(m);
            }


            Console.WriteLine($"[DEBUG] $ Loaded {scene.AnimationCount} animation(s)" +
                $"" +
                $" {scene.MeshCount} meshes ");

            var globalInv = scene.RootNode.Transform;
            globalInv.Inverse();
            this.globalInverseTransform = Utils.FromAssimp(globalInv);

            this.skinnedMeshes = allMeshes.ToArray();
        }

        public SkinnedMesh[] skinnedMeshes;
        public Animation[] animations;
        public Mat4f globalInverseTransform;

        public Tuple<float[], int[]> getVertexData()
        {
            var vertices = new List<float>();
            var indices = new List<int>();

            foreach (var mesh in skinnedMeshes)
            {
                indices.AddRange(mesh.indices);

                foreach (var vertex in mesh.vertices)
                {
                    vertices.Add(vertex.position.X);
                    vertices.Add(vertex.position.Y);
                    vertices.Add(vertex.position.Z);

                    vertices.Add(vertex.normal.X);
                    vertices.Add(vertex.normal.Y);
                    vertices.Add(vertex.normal.Z);

                    vertices.Add(vertex.texCoords.X);
                    vertices.Add(1.0f - vertex.texCoords.Y);

                    vertices.Add(vertex.boneIDs.X);
                    vertices.Add(vertex.boneIDs.Y);
                    vertices.Add(vertex.boneIDs.Z);
                    vertices.Add(vertex.boneIDs.W);

                    vertices.Add(vertex.boneWeights.X);
                    vertices.Add(vertex.boneWeights.Y);
                    vertices.Add(vertex.boneWeights.Z);
                    vertices.Add(vertex.boneWeights.W);
                }
            }

            return Tuple.Create(vertices.ToArray(), indices.ToArray());
        }
    }
}