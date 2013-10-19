﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using BrawlLib.SSBB.ResourceNodes;
using System.Windows.Forms;
using BrawlLib.Wii.Models;
using OpenTK.Graphics.OpenGL;
using BrawlLib.Imaging;
using BrawlLib.Wii.Graphics;

namespace BrawlLib.Modeling
{
    #region PMD Importer & Exporter
    public class PMDModel
    {
        #region Main Importer
        public static MDL0Node ImportModel(string filepath)
        {
            filepath = Path.GetFullPath(filepath);
            if (!File.Exists(filepath))
                throw new FileNotFoundException("PMD model file " + filepath + " not found.");

            MDL0Node model = null;
            using (FileStream fs = new FileStream(filepath, FileMode.Open))
            {
                BinaryReader reader = new BinaryReader(fs);
                string magic = encoder.GetString(reader.ReadBytes(3));
                if (magic != "Pmd")
                    throw new FileLoadException("Model format not recognized.");

                float version = BitConverter.ToSingle(reader.ReadBytes(4), 0);
                if (version == 1.0f)
                    model = new MDL0Node();
                else
                    throw new Exception("Version " + version.ToString() + " models are not supported.");

                if (model != null)
                {
                    Read(reader, CoordinateType.LeftHanded); //Will flip model backwards if right handed
                    PMD2MDL0(model);
                }
                fs.Close();
            }
            return model;
        }
        #endregion

        #region Main Exporter
        public static void Export(MDL0Node model, string filename)
        {
            filename = Path.GetFullPath(filename);
            using (FileStream fs = new FileStream(filename, FileMode.Create))
            {
                BinaryWriter writer = new BinaryWriter(fs);
                writer.Write(PMDModel.encoder.GetBytes("Pmd"));
                writer.Write(1.0f); //Version
                PMDModel.MDL02PMD(model);
                PMDModel.Write(writer);
                fs.Close();
            }
        }
        #endregion

        #region Data Handlers
        internal static Encoding encoder = Encoding.GetEncoding("shift-jis");
        internal static string GetString(byte[] bytes)
        {
            int i;
            for (i = 0; i < bytes.Length; i++)
                if (bytes[i] == 0)
                    break;
            if (i < bytes.Length)
                return encoder.GetString(bytes, 0, i);
            return encoder.GetString(bytes);
        }
        internal static byte[] GetBytes(string input, long size)
        {
            byte[] result = new byte[size];
            for (long i = 0; i < size; i++)
                result[i] = 0;
            if (input == "")
                return result;
            byte[] strs = encoder.GetBytes(input);
            for (long i = 0; i < strs.LongLength; i++)
                if (i < result.LongLength)
                    result[i] = strs[i];
            if (result.LongLength <= strs.LongLength)
                return result;
            result[strs.LongLength] = 0;
            for (long i = strs.LongLength + 1; i < result.Length; i++)
                result[i] = 0xFD;
            return result;
        }
        public enum CoordinateType
        {
            //MMD standard coordinate system
            RightHanded = 1,
            //XNA standard coordinate system
            LeftHanded = -1,
        }
        #endregion

        #region Members and Properties

        public static float _version = 1.0f;
        public static ModelHeader _header;
        public static ModelVertex[] _vertices;
        public static UInt16[] _faceIndices;
        public static ModelMaterial[] _materials;
        public static ModelBone[] _bones;
        public static ModelIK[] _IKs;
        public static ModelSkin[] _skins;
        public static UInt16[] _skinIndex;
        public static ModelBoneDispName[] _boneDispNames;
        public static ModelBoneDisp[] _boneDisps;
        public static bool _expansion;
        public static bool _toonExpansion;
        public static string[] _toonFileNames;
        public const int _numToonFileName = 10;
        public static bool _physicsExpansion;
        public static ModelRigidBody[] _rigidBodies;
        public static ModelJoint[] _joints;
        public static CoordinateType _coordinate;
        
        public static float CoordZ { get { return (float)_coordinate; } }

        #endregion

        #region Main Data Reader & Writer
        public static void Read(BinaryReader reader, CoordinateType coordinate)
        {
            _coordinate = coordinate;

            _header = new ModelHeader();
            _header.Read(reader);

            //Read Vertices
            UInt32 num_vertex = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            _vertices = new ModelVertex[num_vertex];
            for (UInt32 i = 0; i < num_vertex; i++)
            {
                _vertices[i] = new ModelVertex();
                _vertices[i].Read(reader, CoordZ);
            }

            //Read Primitives
            UInt32 face_vert_count = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            _faceIndices = new UInt16[face_vert_count];
            for (UInt32 i = 0; i < face_vert_count; i++)
                _faceIndices[i] = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            
            //Read Materials
            UInt32 material_count = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            _materials = new ModelMaterial[material_count];
            for (UInt32 i = 0; i < material_count; i++)
            {
                _materials[i] = new ModelMaterial();
                _materials[i].Read(reader);
            }

            //Read Bones
            UInt16 bone_count = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _bones = new ModelBone[bone_count];
            for (UInt16 i = 0; i < bone_count; i++)
            {
                _bones[i] = new ModelBone();
                _bones[i].Read(reader, CoordZ);
            }

            //Read IK Bones
            UInt16 ik_count = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _IKs = new ModelIK[ik_count];
            for (UInt16 i = 0; i < ik_count; i++)
            {
                _IKs[i] = new ModelIK();
                _IKs[i].Read(reader);
            }

            //Read Face Morphs
            UInt16 skin_count = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _skins = new ModelSkin[skin_count];
            for (UInt16 i = 0; i < skin_count; i++)
            {
                _skins[i] = new ModelSkin();
                _skins[i].Read(reader, CoordZ);
            }

            //Read face morph indices
            byte skin_disp_count = reader.ReadByte();
            _skinIndex = new UInt16[skin_disp_count];
            for (byte i = 0; i < _skinIndex.Length; i++)
                _skinIndex[i] = BitConverter.ToUInt16(reader.ReadBytes(2), 0);

            //Read bone morph names
            byte bone_disp_name_count = reader.ReadByte();
            _boneDispNames = new ModelBoneDispName[bone_disp_name_count];
            for (byte i = 0; i < _boneDispNames.Length; i++)
            {
                _boneDispNames[i] = new ModelBoneDispName();
                _boneDispNames[i].Read(reader);
            }

            //Read bone morphs
            UInt32 bone_disp_count = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            _boneDisps = new ModelBoneDisp[bone_disp_count];
            for (UInt32 i = 0; i < _boneDisps.Length; i++)
            {
                _boneDisps[i] = new ModelBoneDisp();
                _boneDisps[i].Read(reader);
            }

            //Read English strings, if there are any.
            try
            {
                _expansion = (reader.ReadByte() != 0);
                if (_expansion)
                {
                    _header.ReadExpansion(reader);
                    for (UInt16 i = 0; i < bone_count; i++)
                    {
                        _bones[i].ReadExpansion(reader);
                    }
                    for (UInt16 i = 0; i < skin_count; i++)
                    {
                        if (_skins[i]._skinType != 0)
                            _skins[i].ReadExpansion(reader);
                    }
                    for (byte i = 0; i < _boneDispNames.Length; i++)
                    {
                        _boneDispNames[i].ReadExpansion(reader);
                    }
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        _toonExpansion = false;
                    else
                    {
                        _toonExpansion = true;
                        _toonFileNames = new string[_numToonFileName];
                        for (int i = 0; i < _toonFileNames.Length; i++)
                        {
                            _toonFileNames[i] = GetString(reader.ReadBytes(100));
                        }
                    }
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        _physicsExpansion = false;
                    else
                    {
                        _physicsExpansion = true;
                        UInt32 rididbody_count = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                        _rigidBodies = new ModelRigidBody[rididbody_count];
                        for (UInt32 i = 0; i < rididbody_count; i++)
                        {
                            _rigidBodies[i] = new ModelRigidBody();
                            _rigidBodies[i].ReadExpansion(reader, CoordZ);
                        }
                        UInt32 joint_count = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
                        _joints = new ModelJoint[joint_count];
                        for (UInt32 i = 0; i < joint_count; i++)
                        {
                            _joints[i] = new ModelJoint();
                            _joints[i].ReadExpansion(reader, CoordZ);
                        }
                    }
                }
            }
            catch { Console.WriteLine("This file does not contain English strings."); }
        }
        public static void Write(BinaryWriter writer)
        {
            //通常ヘッダ書きだし(英語ヘッダはBoneIndexの後(ミクなら0x00071167)に書かれている
            if (_header != null)
                _header.Write(writer);
            //頂点リスト書きだし
            if (_vertices == null)
                writer.Write((UInt32)0);
            else
            {
                writer.Write((UInt32)_vertices.LongLength);
                for (UInt32 i = 0; i < _vertices.LongLength; i++)
                {
                    if (_vertices[i] == null)
                        throw new ArgumentNullException("Vertexes[" + i.ToString() + "] is null!");
                    _vertices[i].Write(writer, CoordZ);
                }
            }
            //面リスト書きだし
            if (_faceIndices == null)
                writer.Write((UInt32)0);
            else
            {
                writer.Write((UInt32)_faceIndices.LongLength);
                for (UInt32 i = 0; i < _faceIndices.LongLength; i++)
                {
                    writer.Write(_faceIndices[i]);
                }
            }
            //材質リスト書きだし
            if (_materials == null)
                writer.Write((UInt32)0);
            else
            {
                writer.Write((UInt32)_materials.LongLength);
                for (UInt32 i = 0; i < _materials.LongLength; i++)
                {
                    if (_materials[i] == null)
                        throw new ArgumentNullException("Materials[" + i.ToString() + "] is null!");
                    _materials[i].Write(writer);
                }
            }
            //ボーンリスト書きだし
            if (_bones == null)
                writer.Write((UInt16)0);
            else
            {
                writer.Write((UInt16)_bones.Length);
                for (UInt16 i = 0; i < _bones.Length; i++)
                {
                    if (_bones[i] == null)
                        throw new ArgumentNullException("Bones[" + i.ToString() + "] is null!");
                    _bones[i].Write(writer, CoordZ);
                }
            }
            //IKリスト書きだし
            if (_IKs == null)
                writer.Write((UInt16)0);
            else
            {
                writer.Write((UInt16)_IKs.Length);
                for (UInt16 i = 0; i < _IKs.Length; i++)
                {
                    if (_IKs[i] == null)
                        throw new ArgumentNullException("IKs[" + i.ToString() + "] is null!");
                    _IKs[i].Write(writer);
                }
            }
            //表情リスト書きだし
            if (_skins == null)
                writer.Write((UInt16)0);
            else
            {
                writer.Write((UInt16)_skins.Length);
                for (UInt16 i = 0; i < _skins.Length; i++)
                {
                    if (_skins[i] == null)
                        throw new ArgumentNullException("Skins[" + i.ToString() + "] is null!");
                    _skins[i].Write(writer, CoordZ);
                }
            }
            //表情枠用表示リスト書きだし
            if (_skinIndex == null)
                writer.Write((byte)0);
            else
            {
                writer.Write((byte)_skinIndex.Length);

                for (byte i = 0; i < _skinIndex.Length; i++)
                {
                    writer.Write(_skinIndex[i]);
                }
            }
            //ボーン枠用枠名リスト
            if (_boneDispNames == null)
                writer.Write((byte)0);
            else
            {
                writer.Write((byte)_boneDispNames.Length);
                for (byte i = 0; i < _boneDispNames.Length; i++)
                {
                    if (_boneDispNames[i] == null)
                        throw new ArgumentNullException("BoneDispNames[" + i.ToString() + "] is null!");
                    _boneDispNames[i].Write(writer);
                }
            }
            //ボーン枠用表示リスト
            if (_boneDisps == null)
                writer.Write((UInt32)0);
            else
            {
                writer.Write((UInt32)_boneDisps.Length);
                for (UInt32 i = 0; i < _boneDisps.Length; i++)
                {
                    if (_boneDisps[i] == null)
                        throw new ArgumentNullException("BoneDisps[" + i.ToString() + "] is null!");
                    _boneDisps[i].Write(writer);
                }
            }
            //英語表記フラグ
            writer.Write((byte)(_expansion ? 1 : 0));
            if (_expansion)
            {
                //英語ヘッダ
                _header.WriteExpansion(writer);
                //ボーンリスト(英語)
                if (_bones != null)
                {
                    for (UInt16 i = 0; i < _bones.Length; i++)
                    {
                        _bones[i].WriteExpansion(writer);
                    }
                }
                //スキンリスト(英語)
                if (_skins != null)
                {
                    for (UInt16 i = 0; i < _skins.Length; i++)
                    {
                        if (_skins[i]._skinType != 0)//baseのスキンには英名無し
                            _skins[i].WriteExpansion(writer);
                    }
                }
                //ボーン枠用枠名リスト(英語)
                if (_boneDispNames != null)
                {
                    for (byte i = 0; i < _boneDispNames.Length; i++)
                    {
                        _boneDispNames[i].WriteExpansion(writer);
                    }
                }
                if (_toonExpansion)
                {
                    //トゥーンテクスチャリスト
                    for (int i = 0; i < _toonFileNames.Length; i++)
                    {
                        writer.Write(GetBytes(_toonFileNames[i], 100));
                    }
                    if (_physicsExpansion)
                    {
                        //剛体リスト
                        if (_rigidBodies == null)
                            writer.Write((UInt32)0);
                        else
                        {
                            writer.Write((UInt32)_rigidBodies.LongLength);
                            for (long i = 0; i < _rigidBodies.LongLength; i++)
                            {
                                if (_rigidBodies[i] == null)
                                    throw new ArgumentNullException("RididBodies[" + i.ToString() + "] is null!");
                                _rigidBodies[i].WriteExpansion(writer, CoordZ);
                            }
                        }
                        //ジョイントリスト
                        if (_joints == null)
                            writer.Write((UInt32)0);
                        else
                        {
                            writer.Write((UInt32)_joints.LongLength);
                            for (long i = 0; i < _joints.LongLength; i++)
                            {
                                if (_joints[i] == null)
                                    throw new ArgumentNullException("Joints[" + i.ToString() + "] is null!");
                                _joints[i].WriteExpansion(writer, CoordZ);
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region PMD to MDL0
        public static unsafe void PMD2MDL0(MDL0Node model)
        {
            model._version = 9;
            model._needsNrmMtxArray = model._needsTexMtxArray = 1;
            model._isImport = true;
            model._importOptions._forceCCW = true;
            model._importOptions._fltVerts = true;
            model._importOptions._fltNrms = true;
            model._importOptions._fltUVs = true;

            model.InitGroups();

            List<MDL0BoneNode> BoneCache = new List<MDL0BoneNode>();

            int index = 0;
            if (!String.IsNullOrWhiteSpace(_header._modelNameEnglish))
                model.Name = _header._modelNameEnglish;
            else
                model.Name = _header._modelName;

            if (!String.IsNullOrWhiteSpace(_header._commentEnglish))
                MessageBox.Show(_header._commentEnglish);
            else
                MessageBox.Show(_header._comment);

            ModelBone parent = null;
            foreach (ModelBone b in _bones)
            {
                MDL0BoneNode bone = new MDL0BoneNode();

                if (!String.IsNullOrWhiteSpace(b._boneNameEnglish))
                    bone._name = b._boneNameEnglish;
                else
                    bone._name = b._boneName;

                bone._entryIndex = index++;

                if (b._parentBoneIndex != ushort.MaxValue)
                {
                    parent = _bones[b._parentBoneIndex];
                    foreach (MDL0BoneNode v in model._boneGroup._children)
                        AssignParent(v, b, bone, parent);
                }
                else
                {
                    bone.Parent = model._boneGroup;
                    bone._bindState._scale = new Vector3(1.0f);
                    bone._bindState._translate = new Vector3(b._wPos[0], b._wPos[1], b._wPos[2]);
                    bone._bindState.CalcTransforms();
                    bone.RecalcBindState();
                }

                BoneCache.Add(bone);
            }

            MDL0ShaderNode texSpa = null, tex = null, colorSpa = null, color = null;

            index = 0;
            foreach (ModelMaterial m in _materials)
            {
                MDL0MaterialNode mn = new MDL0MaterialNode();
                mn.Name = "Material" + index++;

                MDL0MaterialRefNode texRef = null;
                MDL0MaterialRefNode spaRef = null;

                if (!String.IsNullOrEmpty(m._textureFileName))
                    if (m._textureFileName.Contains('*'))
                    {
                        string[] names = m._textureFileName.Split('*');
                        if (!String.IsNullOrEmpty(names[0]))
                        {
                            texRef = new MDL0MaterialRefNode();
                            texRef.Name = names[0].Substring(0, names[0].IndexOf('.'));
                        }
                        if (!String.IsNullOrEmpty(names[1]))
                        {
                            spaRef = new MDL0MaterialRefNode();
                            spaRef.Name = names[1].Substring(0, names[1].IndexOf('.'));
                            spaRef.MapMode = MDL0MaterialRefNode.MappingMethod.EnvCamera;
                            spaRef.UWrapMode = MDL0MaterialRefNode.WrapMode.Clamp;
                            spaRef.VWrapMode = MDL0MaterialRefNode.WrapMode.Clamp;
                            spaRef.Projection = Wii.Graphics.TexProjection.STQ;
                            spaRef.InputForm = Wii.Graphics.TexInputForm.ABC1;
                            spaRef.Coordinates = Wii.Graphics.TexSourceRow.Normals;
                            spaRef.Normalize = true;
                        }
                    }
                    else
                    {
                        texRef = new MDL0MaterialRefNode();
                        texRef.Name = m._textureFileName.Substring(0, m._textureFileName.IndexOf('.'));
                        texRef.Coordinates = Wii.Graphics.TexSourceRow.TexCoord0;
                    }

                if (texRef != null)
                {
                    (texRef._texture = model.FindOrCreateTexture(texRef.Name))._references.Add(texRef);
                    texRef._parent = mn;
                    mn._children.Add(texRef);
                }
                if (spaRef != null)
                {
                    (spaRef._texture = model.FindOrCreateTexture(spaRef.Name))._references.Add(spaRef);
                    spaRef._parent = mn;
                    mn._children.Add(spaRef);
                }

                mn._chan1._matColor = new RGBAPixel((byte)(m._diffuseColor[0] * 255), (byte)(m._diffuseColor[1] * 255), (byte)(m._diffuseColor[2] * 255), 255);
                mn._chan1.ColorMaterialSource = GXColorSrc.Register;

                if (texRef != null && spaRef != null)
                {
                    if (texSpa == null)
                    {
                        MDL0ShaderNode n = TexSpaShader;
                        n._parent = model._shadGroup;
                        model._shadList.Add(n);
                        texSpa = n;
                    }
                    mn.ShaderNode = texSpa;
                }
                else if (texRef != null)
                {
                    if (tex == null)
                    {
                        MDL0ShaderNode n = TexShader;
                        n._parent = model._shadGroup;
                        model._shadList.Add(n);
                        tex = n;
                    }
                    mn.ShaderNode = tex;
                }
                else if (spaRef != null)
                {
                    if (colorSpa == null)
                    {
                        MDL0ShaderNode n = ColorSpaShader;
                        n._parent = model._shadGroup;
                        model._shadList.Add(n);
                        colorSpa = n;
                    }
                    mn.ShaderNode = colorSpa;
                }
                else
                {
                    if (color == null)
                    {
                        MDL0ShaderNode n = ColorShader;
                        n._parent = model._shadGroup;
                        model._shadList.Add(n);
                        color = n;
                    }
                    mn.ShaderNode = color;
                }

                mn._parent = model._matGroup;
                model._matList.Add(mn);
            }

            int x = 0;
            int offset = 0;
            foreach (ModelMaterial m in _materials)
            {
                PrimitiveManager manager = new PrimitiveManager() { _pointCount = (int)m._faceVertCount };
                MDL0ObjectNode p = new MDL0ObjectNode() { _manager = manager, _opaMaterial = (MDL0MaterialNode)model._matList[x] };
                p._opaMaterial._objects.Add(p);
                p._manager._vertices = new List<Vertex3>();
                p.Name = "polygon" + x++;
                p._parent = model._objGroup;

                p._manager._indices = new UnsafeBuffer((int)m._faceVertCount * 2);
                p._manager._faceData[0] = new UnsafeBuffer((int)m._faceVertCount * 12);
                p._manager._faceData[1] = new UnsafeBuffer((int)m._faceVertCount * 12);
                p._manager._faceData[4] = new UnsafeBuffer((int)m._faceVertCount * 8);

                ushort* Indices = (ushort*)p._manager._indices.Address;
                Vector3* Vertices = (Vector3*)p._manager._faceData[0].Address;
                Vector3* Normals = (Vector3*)p._manager._faceData[1].Address;
                Vector2* UVs = (Vector2*)p._manager._faceData[4].Address;

                manager._triangles = new NewPrimitive((int)m._faceVertCount, BeginMode.Triangles);
                uint* pTri = (uint*)manager._triangles._indices.Address;

                index = 0;
                List<int> usedVertices = new List<int>();
                List<int> vertexIndices = new List<int>();
                for (int s = offset, l = 0; l < (int)m._faceVertCount; l++, s++)
                {
                    ushort i = _faceIndices[s];
                    ModelVertex mv = _vertices[i];
                    ushort j = 0;
                    if (!usedVertices.Contains(i))
                    {
                        Influence inf;
                        BoneWeight weight1 = null, weight2 = null;

                        float weight = ((float)mv._boneWeight / 100.0f).Clamp(0.0f, 1.0f);

                        if (weight > 0.0f && weight < 1.0f)
                        {
                            weight1 = new BoneWeight(BoneCache[mv._boneIndex[0]], weight);
                            weight2 = new BoneWeight(BoneCache[mv._boneIndex[1]], 1.0f - weight);
                        }
                        else if (weight == 0.0f)
                            weight1 = new BoneWeight(BoneCache[mv._boneIndex[1]]);
                        else
                            weight1 = new BoneWeight(BoneCache[mv._boneIndex[0]]);

                        if (weight2 != null)
                            inf = new Influence(new List<BoneWeight> { weight1, weight2 });
                        else
                            inf = new Influence(new List<BoneWeight> { weight1 });

                        Vector3 t = new Vector3();
                        Vertex3 v;
                        t._x = mv._position[0];
                        t._y = mv._position[1];
                        t._z = mv._position[2];
                        if (inf._weights.Count > 1)
                        {
                            inf = model._influences.FindOrCreate(inf, true);
                            inf.CalcMatrix();
                            v = new Vertex3(t, inf);
                        }
                        else
                        {
                            MDL0BoneNode bone = inf._weights[0].Bone;
                            v = new Vertex3(t * bone._inverseBindMatrix, bone);
                        }

                        p._manager._vertices.Add(v);
                        vertexIndices.Add(usedVertices.Count);
                        usedVertices.Add(i);
                        j = (ushort)(usedVertices.Count - 1);
                    }
                    else
                        j = (ushort)vertexIndices[usedVertices.IndexOf(i)];

                    *Indices++ = j;
                    *pTri++ = (uint)l;
                    *Vertices++ = p._manager._vertices[j]._position;
                    *Normals++ = mv._normal;
                    *UVs++ = mv._texCoord;
                }
                model._objList.Add(p);
                offset += (int)m._faceVertCount;
            }

            //Check each polygon to see if it can be rigged to a single influence
            if (model._objList != null && model._objList.Count != 0)
                foreach (MDL0ObjectNode p in model._objList)
                {
                    IMatrixNode node = null;
                    bool singlebind = true;

                    foreach (Vertex3 v in p._manager._vertices)
                        if (v._matrixNode != null)
                        {
                            if (node == null)
                                node = v._matrixNode;

                            if (v._matrixNode != node)
                            {
                                singlebind = false;
                                break;
                            }
                        }

                    if (singlebind && p._matrixNode == null)
                    {
                        //Increase reference count ahead of time for rebuild
                        if (p._manager._vertices[0]._matrixNode != null)
                            p._manager._vertices[0]._matrixNode.ReferenceCount++;

                        foreach (Vertex3 v in p._manager._vertices)
                            if (v._matrixNode != null)
                                v._matrixNode.ReferenceCount--;

                        p._nodeId = -2; //Continued on polygon rebuild
                    }
                }

            model.CleanGroups();
            model.Rebuild(true);
            //model.BuildFromScratch(null);
        }
        public static void AssignParent(MDL0BoneNode pBone, ModelBone child, MDL0BoneNode cBone, ModelBone parent)
        {
            if (pBone._entryIndex == child._parentBoneIndex)
            {
                //Link child to its parent
                (cBone._parent = pBone)._children.Add(cBone);

                //Convert the world point into a local point relative to the bone's parent
                Matrix m = (Matrix.TranslationMatrix(child._wPos) * Matrix.Invert(Matrix.TranslationMatrix(parent._wPos)));

                //Derive to state and recalc bind matrices
                cBone._bindState = m.Derive();
                cBone.RecalcBindState();
            }
            else //Parent not found, continue searching children.
                foreach (MDL0BoneNode pMatch in pBone._children)
                    AssignParent(pMatch, child, cBone, parent);
        }

        public static MDL0ShaderNode TexSpaShader
        {
            get
            {
                MDL0ShaderNode shader = new MDL0ShaderNode();
                shader.TextureRef0 = true;
                shader.TextureRef1 = true;

                TEVStage s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x8FFF8;
                s._alphaEnv = 0x8FFC0;
                s.KonstantColorSelection = TevKColorSel.KSel_3_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_3_Alpha;
                s.TextureMapID = TexMapID.TexMap0;
                s.TextureCoord = TexCoordID.TexCoord0;
                s.TextureEnabled = true;
                s.ColorChannel = ColorSelChan.Zero;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x88E80;
                s._alphaEnv = 0x8FF80;
                s.KonstantColorSelection = TevKColorSel.Constant1_4;
                s.KonstantAlphaSelection = TevKAlphaSel.Constant1_4;
                s.TextureMapID = TexMapID.TexMap1;
                s.TextureCoord = TexCoordID.TexCoord1;
                s.TextureEnabled = true;
                s.ColorChannel = ColorSelChan.Zero;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x28F0AF;
                s._alphaEnv = 0x8FF80;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x8FEB0;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_1_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x806EF;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = true;
                s.ColorChannel = ColorSelChan.Zero;

                return shader;
            }
        }
        public static MDL0ShaderNode ColorSpaShader
        {
            get
            {
                MDL0ShaderNode shader = new MDL0ShaderNode();
                shader.TextureRef0 = true;

                TEVStage s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x8FFFA;
                s._alphaEnv = 0x8FFD0;
                s.KonstantColorSelection = TevKColorSel.KSel_3_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_3_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;
                
                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x88E80;
                s._alphaEnv = 0x8FF80;
                s.KonstantColorSelection = TevKColorSel.Constant1_4;
                s.KonstantAlphaSelection = TevKAlphaSel.Constant1_4;
                s.TextureMapID = TexMapID.TexMap0;
                s.TextureCoord = TexCoordID.TexCoord0;
                s.TextureEnabled = true;
                s.ColorChannel = ColorSelChan.Zero;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x28F0AF;
                s._alphaEnv = 0x8FF80;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x8FEB0;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_1_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x806EF;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = true;
                s.ColorChannel = ColorSelChan.Zero;

                return shader;
            }
        }
        public static MDL0ShaderNode TexShader
        {
            get
            {
                MDL0ShaderNode shader = new MDL0ShaderNode();
                shader.TextureRef0 = true;

                TEVStage s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x28FFF8;
                s._alphaEnv = 0x8FFC0;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap0;
                s.TextureCoord = TexCoordID.TexCoord0;
                s.TextureEnabled = true;
                s.ColorChannel = ColorSelChan.Zero;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x8FEB0;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_1_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x806EF;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.Zero;

                return shader;
            }
        }
        public static MDL0ShaderNode ColorShader
        {
            get
            {
                MDL0ShaderNode shader = new MDL0ShaderNode();

                TEVStage s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x28FFFA;
                s._alphaEnv = 0x8FFD0;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x8FEB0;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_1_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.ColorChannel0;

                s = new TEVStage();
                shader.AddChild(s);

                s._colorEnv = 0x806EF;
                s._alphaEnv = 0x81FF0;
                s.KonstantColorSelection = TevKColorSel.KSel_0_Value;
                s.KonstantAlphaSelection = TevKAlphaSel.KSel_0_Alpha;
                s.TextureMapID = TexMapID.TexMap7;
                s.TextureCoord = TexCoordID.TexCoord7;
                s.TextureEnabled = false;
                s.ColorChannel = ColorSelChan.Zero;

                return shader;
            }
        }

        #endregion

        #region MDL0 to PMD
        public static unsafe void MDL02PMD(MDL0Node model)
        {
            _header = new ModelHeader();
            _header._modelName = model.Name;

            //To do: Add the ability to change the comment
            _header._comment = "MDL0 model converted to PMD by Brawlbox.";

            foreach (MDL0MaterialNode m in model._matList)
            {
                ModelMaterial mat = new ModelMaterial();
                mat._textureFileName = m.Children[0].Name;
                
            }

            _bones = new ModelBone[model._linker.BoneCache.Length];
            for (int i = 0; i < model._linker.BoneCache.Length; i++)
            {
                ModelBone bone = new ModelBone();
                MDL0BoneNode mBone = (MDL0BoneNode)model._linker.BoneCache[i];

                Vector3 wPos = mBone._bindMatrix.GetPoint();
                bone._wPos[0] = wPos._x;
                bone._wPos[1] = wPos._y;
                bone._wPos[2] = wPos._z;
              
                bone._boneName = mBone.Name;

                bone._boneType = 0;
                bone._parentBoneIndex = (ushort)model._linker.BoneCache.ToList<ResourceNode>().IndexOf(mBone.Parent);

                _bones[i] = bone;
            }
        }
        #endregion
    }
    #endregion

    #region PMD Classes

    #region Model Header
    public class ModelHeader
    {
        public string _modelName;
        public string _comment;
        public string _modelNameEnglish;
        public string _commentEnglish;

        public ModelHeader()
        {
            _modelName = "";
            _comment = "";
            _modelNameEnglish = null;
            _commentEnglish = null;
        }

        internal void Read(BinaryReader reader)
        {
            _modelName = PMDModel.GetString(reader.ReadBytes(20));
            _comment = PMDModel.GetString(reader.ReadBytes(256));
        }

        internal void ReadExpansion(BinaryReader reader)
        {
            _modelNameEnglish = PMDModel.GetString(reader.ReadBytes(20));
            _commentEnglish = PMDModel.GetString(reader.ReadBytes(256));
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(PMDModel.GetBytes(_modelName, 20));
            writer.Write(PMDModel.GetBytes(_comment, 256));
        }

        internal void WriteExpansion(BinaryWriter writer)
        {
            writer.Write(PMDModel.GetBytes(_modelNameEnglish, 20));
            writer.Write(PMDModel.GetBytes(_commentEnglish, 256));
        }
    }
    #endregion

    #region Model Vertex
    public class ModelVertex
    {
        public Vector3 _position;
        public Vector3 _normal;
        public Vector2 _texCoord;
        public UInt16[] _boneIndex;
        public byte _boneWeight;
        public byte _edgeFlag;

        public ModelVertex()
        {
            _position = new Vector3();
            _normal = new Vector3();
            _texCoord = new Vector2();
            _boneIndex = new UInt16[2];
            _boneWeight = 0;
            _edgeFlag = 0;
        }

        internal void Read(BinaryReader reader, float CoordZ)
        {
            _position = new Vector3();
            _normal = new Vector3();
            _texCoord = new Vector2();
            _boneIndex = new UInt16[2];
            _position = new Vector3(BitConverter.ToSingle(reader.ReadBytes(4), 0), BitConverter.ToSingle(reader.ReadBytes(4), 0), BitConverter.ToSingle(reader.ReadBytes(4), 0));
            _normal = new Vector3(BitConverter.ToSingle(reader.ReadBytes(4), 0), BitConverter.ToSingle(reader.ReadBytes(4), 0), BitConverter.ToSingle(reader.ReadBytes(4), 0));
            _texCoord = new Vector2(BitConverter.ToSingle(reader.ReadBytes(4), 0), BitConverter.ToSingle(reader.ReadBytes(4), 0));
            for (int i = 0; i < _boneIndex.Length; i++)
                _boneIndex[i] = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _boneWeight = reader.ReadByte();
            _edgeFlag = reader.ReadByte();
            _position[2] = _position[2] * CoordZ;
            _normal[2] = _normal[2] * CoordZ;
        }

        internal void Write(BinaryWriter writer, float CoordZ)
        {
            _position[2] = _position[2] * CoordZ;
            _normal[2] = _normal[2] * CoordZ;
            writer.Write(_position._x);
            writer.Write(_position._y);
            writer.Write(_position._z);
            writer.Write(_normal._x);
            writer.Write(_normal._y);
            writer.Write(_normal._z);
            writer.Write(_texCoord._x);
            writer.Write(_texCoord._y);
            for (int i = 0; i < _boneIndex.Length; i++)
                writer.Write(_boneIndex[i]);
            writer.Write(_boneWeight);
            writer.Write(_edgeFlag);
        }
    }
    #endregion

    #region Model Material
    public class ModelMaterial
    {
        public float[] _diffuseColor;
        public float _alpha;
        public float _specularity;
        public float[] _specularColor;
        public float[] _mirrorColor;
        public byte _toonIndex;
        public byte _edgeFlag;
        public UInt32 _faceVertCount;
        public string _textureFileName;

        public ModelMaterial()
        {
            _diffuseColor = new float[3];
            _specularColor = new float[3];
            _mirrorColor = new float[3];
        }

        internal void Read(BinaryReader reader)
        {
            _diffuseColor = new float[3];
            _specularColor = new float[3];
            _mirrorColor = new float[3];
            for (int i = 0; i < _diffuseColor.Length; i++)
                _diffuseColor[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _alpha = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _specularity = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _specularColor.Length; i++)
                _specularColor[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _mirrorColor.Length; i++)
                _mirrorColor[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _toonIndex = reader.ReadByte();
            _edgeFlag = reader.ReadByte();
            _faceVertCount = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            _textureFileName = PMDModel.GetString(reader.ReadBytes(20));
        }

        internal void Write(BinaryWriter writer)
        {
            for (int i = 0; i < _diffuseColor.Length; i++)
                writer.Write(_diffuseColor[i]);
            writer.Write(_alpha);
            writer.Write(_specularity);
            for (int i = 0; i < _specularColor.Length; i++)
                writer.Write(_specularColor[i]);
            for (int i = 0; i < _mirrorColor.Length; i++)
                writer.Write(_mirrorColor[i]);
            writer.Write(_toonIndex);
            writer.Write(_edgeFlag);
            writer.Write(_faceVertCount);
            writer.Write(PMDModel.GetBytes(_textureFileName, 20));
        }
    }
    #endregion

    #region Model Bone
    public class ModelBone
    {
        public string _boneName;
        public UInt16 _parentBoneIndex;
        public UInt16 _tailPosBoneIndex;
        public byte _boneType;
        public UInt16 _IKParentBoneIndex;
        public Vector3 _wPos;
        public string _boneNameEnglish;

        public ModelBone()
        {
            _wPos = new Vector3();
        }

        internal void Read(BinaryReader reader, float CoordZ)
        {
            _boneName = PMDModel.GetString(reader.ReadBytes(20));
            _parentBoneIndex = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _tailPosBoneIndex = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _boneType = reader.ReadByte();
            _IKParentBoneIndex = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _wPos = new Vector3(BitConverter.ToSingle(reader.ReadBytes(4), 0), BitConverter.ToSingle(reader.ReadBytes(4), 0), BitConverter.ToSingle(reader.ReadBytes(4), 0));
            _boneNameEnglish = null;
            _wPos[2] = _wPos[2] * CoordZ;
        }

        internal void ReadExpansion(BinaryReader reader)
        {
            _boneNameEnglish = PMDModel.GetString(reader.ReadBytes(20));
        }

        internal void Write(BinaryWriter writer, float CoordZ)
        {
            _wPos[2] = _wPos[2] * CoordZ;
            writer.Write(PMDModel.GetBytes(_boneName, 20));
            writer.Write(_parentBoneIndex);
            writer.Write(_tailPosBoneIndex);
            writer.Write(_boneType);
            writer.Write(_IKParentBoneIndex);
            writer.Write(_wPos._x);
            writer.Write(_wPos._y);
            writer.Write(_wPos._z);
        }
        
        internal void WriteExpansion(BinaryWriter writer)
        {
            writer.Write(PMDModel.GetBytes(_boneNameEnglish, 20));
        }
    }
    public class ModelBoneDisp
    {
        public UInt16 _boneIndex;
        public byte _boneDispFrameIndex;

        internal void Read(BinaryReader reader)
        {
            _boneIndex = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _boneDispFrameIndex = reader.ReadByte();
        }
        internal void Write(BinaryWriter writer)
        {
            writer.Write(_boneIndex);
            writer.Write(_boneDispFrameIndex);
        }
    }
    public class ModelBoneDispName
    {
        public string _boneDispName;
        public string _boneDispNameEnglish;

        internal void Read(BinaryReader reader)
        {
            _boneDispName = PMDModel.GetString(reader.ReadBytes(50));
            _boneDispNameEnglish = null;
        }
        internal void ReadExpansion(BinaryReader reader)
        {
            _boneDispNameEnglish = PMDModel.GetString(reader.ReadBytes(50));
        }
        internal void Write(BinaryWriter writer)
        {
            writer.Write(PMDModel.GetBytes(_boneDispName, 50));
        }

        internal void WriteExpansion(BinaryWriter writer)
        {
            writer.Write(PMDModel.GetBytes(_boneDispNameEnglish, 50));
        }
    }
    #endregion

    #region Model Joint
    public class ModelJoint
    {
        //Rotations in radians

        public string _name;
        public UInt32 _rigidBodyA;
        public UInt32 _rigidBodyB;
        public float[] _position;
        public float[] _rotation;
        public float[] _constrainPosition1;
        public float[] _constrainPosition2;
        public float[] _constrainRotation1;
        public float[] _constrainRotation2;
        public float[] _springPosition;
        public float[] _springRotation;

        public ModelJoint()
        {
            _position = new float[3];
            _rotation = new float[3];
            _constrainPosition1 = new float[3];
            _constrainPosition2 = new float[3];
            _constrainRotation1 = new float[3];
            _constrainRotation2 = new float[3];
            _springPosition = new float[3];
            _springRotation = new float[3];
        }

        internal void ReadExpansion(BinaryReader reader, float CoordZ)
        {
            _name = PMDModel.GetString(reader.ReadBytes(20));
            _rigidBodyA = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            _rigidBodyB = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            for (int i = 0; i < _position.Length; i++)
                _position[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _rotation.Length; i++)
                _rotation[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _constrainPosition1.Length; i++)
                _constrainPosition1[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _constrainPosition2.Length; i++)
                _constrainPosition2[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _constrainRotation1.Length; i++)
                _constrainRotation1[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _constrainRotation2.Length; i++)
                _constrainRotation2[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _springPosition.Length; i++)
                _springPosition[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _springRotation.Length; i++)
                _springRotation[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _position[2] *= CoordZ;
            _rotation[0] *= CoordZ;
            _rotation[1] *= CoordZ;
            //ConstrainRotation1[0] *= CoordZ;
            //ConstrainRotation1[1] *= CoordZ;
            //ConstrainRotation2[0] *= CoordZ;
            //ConstrainRotation2[1] *= CoordZ;
            //SpringPosition[2] *= CoordZ;
            //SpringRotation[0] *= CoordZ;
            //SpringRotation[1] *= CoordZ;
        }

        internal void WriteExpansion(BinaryWriter writer, float CoordZ)
        {
            _position[2] *= CoordZ;
            _rotation[0] *= CoordZ;
            _rotation[1] *= CoordZ;
            /*ConstrainRotation1[0] *= CoordZ;
            ConstrainRotation1[1] *= CoordZ;
            ConstrainRotation2[0] *= CoordZ;
            ConstrainRotation2[1] *= CoordZ;
            SpringPosition[2] *= CoordZ;
            SpringRotation[0] *= CoordZ;
            SpringRotation[1] *= CoordZ;*/
            writer.Write(PMDModel.GetBytes(_name, 20));
            writer.Write(_rigidBodyA);
            writer.Write(_rigidBodyB);
            for (int i = 0; i < _position.Length; i++)
                writer.Write(_position[i]);
            for (int i = 0; i < _rotation.Length; i++)
                writer.Write(_rotation[i]);
            for (int i = 0; i < _constrainPosition1.Length; i++)
                writer.Write(_constrainPosition1[i]);
            for (int i = 0; i < _constrainPosition2.Length; i++)
                writer.Write(_constrainPosition2[i]);
            for (int i = 0; i < _constrainRotation1.Length; i++)
                writer.Write(_constrainRotation1[i]);
            for (int i = 0; i < _constrainRotation2.Length; i++)
                writer.Write(_constrainRotation2[i]);
            for (int i = 0; i < _springPosition.Length; i++)
                writer.Write(_springPosition[i]);
            for (int i = 0; i < _springRotation.Length; i++)
                writer.Write(_springRotation[i]);
        }
    }
    #endregion

    #region Model IK
    public class ModelIK
    {
        public UInt16 _IKBoneIndex;
        public UInt16 _IKTargetBoneIndex;
        public UInt16 _iterations;
        public float _angleLimit;
        public UInt16[] _IKChildBoneIndex;

        internal void Read(BinaryReader reader)
        {
            _IKBoneIndex = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _IKTargetBoneIndex = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            byte ik_chain_length = reader.ReadByte();
            _iterations = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _angleLimit = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _IKChildBoneIndex = new UInt16[ik_chain_length];
            for (int i = 0; i < ik_chain_length; i++)
                _IKChildBoneIndex[i] = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(_IKBoneIndex);
            writer.Write(_IKTargetBoneIndex);
            writer.Write((byte)_IKChildBoneIndex.Length);
            writer.Write(_iterations);
            writer.Write(_angleLimit);
            for (int i = 0; i < _IKChildBoneIndex.Length; i++)
                writer.Write(_IKChildBoneIndex[i]);
        }
    }
    #endregion

    #region Model Skin Etc
    public class ModelSkinVertexData
    {
        public UInt32 _skinVertIndex;
        public float[] _skinVertPos;

        public ModelSkinVertexData()
        {
            _skinVertPos = new float[3];
        }

        internal void Read(BinaryReader reader, float CoordZ)
        {
            _skinVertIndex = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            for (int i = 0; i < _skinVertPos.Length; i++)
                _skinVertPos[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _skinVertPos[2] *= CoordZ;
        }

        internal void Write(BinaryWriter writer, float CoordZ)
        {
            _skinVertPos[2] *= CoordZ;
            writer.Write(_skinVertIndex);
            for (int i = 0; i < _skinVertPos.Length; i++)
                writer.Write(_skinVertPos[i]);
        }
    }

    //Face & Other Morphs
    public class ModelSkin 
    {
        public string _skinName;
        public byte _skinType;
        public ModelSkinVertexData[] _skinVertDatas;
        public string _skinNameEnglish;

        internal void Read(BinaryReader reader, float CoordZ)
        {
            _skinName = PMDModel.GetString(reader.ReadBytes(20));
            UInt32 skin_vert_count = BitConverter.ToUInt32(reader.ReadBytes(4), 0);
            _skinType = reader.ReadByte();
            _skinVertDatas = new ModelSkinVertexData[skin_vert_count];
            for (int i = 0; i < _skinVertDatas.Length; i++)
            {
                _skinVertDatas[i] = new ModelSkinVertexData();
                _skinVertDatas[i].Read(reader, CoordZ);
            }
            _skinNameEnglish = null;
        }

        internal void ReadExpansion(BinaryReader reader)
        {
            _skinNameEnglish = PMDModel.GetString(reader.ReadBytes(20));
        }

        internal void Write(BinaryWriter writer, float CoordZ)
        {
            writer.Write(PMDModel.GetBytes(_skinName, 20));
            writer.Write((UInt32)_skinVertDatas.Length);
            writer.Write(_skinType);
            for (int i = 0; i < _skinVertDatas.Length; i++)
                _skinVertDatas[i].Write(writer, CoordZ);
        }

        internal void WriteExpansion(BinaryWriter writer)
        {
            writer.Write(PMDModel.GetBytes(_skinNameEnglish, 20));
        }
    }
    #endregion

    #region Model Rigid Body
    public class ModelRigidBody
    {
        public string _name;
        public UInt16 _relatedBoneIndex;
        public byte _groupIndex;
        public UInt16 _groupTarget;
        public byte _shapeType;
        public float _shapeWidth;
        public float _shapeHeight;
        public float _shapeDepth;
        public float[] _position;
        public float[] _rotation;
        public float _weight;
        public float _linearDamping;
        public float _angularDamping;
        public float _restitution;
        public float _friction;
        public byte _type;

        public ModelRigidBody()
        {
            _position = new float[3];
            _rotation = new float[3];
        }

        internal void ReadExpansion(BinaryReader reader, float CoordZ)
        {
            _name = PMDModel.GetString(reader.ReadBytes(20));
            _relatedBoneIndex = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _groupIndex = reader.ReadByte();
            _groupTarget = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            _shapeType = reader.ReadByte();
            _shapeWidth = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _shapeHeight = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _shapeDepth = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _position.Length; i++)
                _position[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            for (int i = 0; i < _rotation.Length; i++)
                _rotation[i] = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _weight = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _linearDamping = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _angularDamping = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _restitution = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _friction = BitConverter.ToSingle(reader.ReadBytes(4), 0);
            _type = reader.ReadByte();
            _position[2] *= CoordZ;
            _rotation[0] *= CoordZ;
            _rotation[1] *= CoordZ;
        }

        internal void WriteExpansion(BinaryWriter writer, float CoordZ)
        {
            _position[2] *= CoordZ;
            _rotation[0] *= CoordZ;
            _rotation[1] *= CoordZ;
            writer.Write(PMDModel.GetBytes(_name, 20));
            writer.Write(_relatedBoneIndex);
            writer.Write(_groupIndex);
            writer.Write(_groupTarget);
            writer.Write(_shapeType);
            writer.Write(_shapeWidth);
            writer.Write(_shapeHeight);
            writer.Write(_shapeDepth);
            for (int i = 0; i < _position.Length; i++)
                writer.Write(_position[i]);
            for (int i = 0; i < _rotation.Length; i++)
                writer.Write(_rotation[i]);
            writer.Write(_weight);
            writer.Write(_linearDamping);
            writer.Write(_angularDamping);
            writer.Write(_restitution);
            writer.Write(_friction);
            writer.Write(_type);
        }
    }
    #endregion

    #endregion
}
