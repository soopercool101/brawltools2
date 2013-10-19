﻿using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BrawlLib.Wii.Animations
{
    public class AnimFormat
    {
        private static readonly string[] types = new string[] { "scale", "rotate", "translate" };
        private static readonly string[] axes = new string[] { "X", "Y", "Z" };
        public static void Serialize(CHR0Node node, bool bake, string output)
        {
            using (StreamWriter file = new StreamWriter(output))
            {
                file.WriteLine("animVersion 1.1;");
                file.WriteLine("mayaVersion 2014 x64;");
                file.WriteLine("timeUnit ntsc;");
                file.WriteLine("linearUnit cm;");
                file.WriteLine("angularUnit deg;");
                file.WriteLine("startTime 1;");
                file.WriteLine(String.Format("endTime {0};", node.FrameCount));
                foreach (CHR0EntryNode e in node.Children)
                {
                    KeyframeCollection c = e.Keyframes;
                    for (int index = 0; index < 9; index++)
                    {
                        KeyFrameMode m = (KeyFrameMode)(index + 0x10);

                        if (c[m] <= 0)
                            continue;
                        
                        file.WriteLine(String.Format("anim {0}.{0}{1} {0}{1} {2} {3} {4} {5}", types[index / 3], axes[index % 3], e.Name, e.Index, index / 3, index % 3));
                        file.WriteLine("animData {");
                        file.WriteLine(" input time;");
                        file.WriteLine(String.Format(" output {0};", index > 2 && index < 6 ? "angular" : "linear"));
                        file.WriteLine(" weighted 1;");
                        file.WriteLine(" preInfinity constant;");
                        file.WriteLine(" postInfinity constant;");
                        file.WriteLine(" keys {");
                        for (KeyframeEntry entry = c._keyRoots[index]._next; (entry != c._keyRoots[index]); entry = entry._next)
                        {
                            float angle = (float)Math.Atan(entry._tangent) * Maths._rad2degf;
                            file.WriteLine(String.Format(" {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10};", 
                                entry._index + 1, 
                                entry._value.ToString(CultureInfo.InvariantCulture.NumberFormat), 
                                "fixed",
                                "fixed",
                                "1",
                                "1",
                                "0",
                                angle.ToString(CultureInfo.InvariantCulture.NumberFormat),
                                (Math.Abs(entry._tangent) + 1).ToString(CultureInfo.InvariantCulture.NumberFormat),
                                angle.ToString(CultureInfo.InvariantCulture.NumberFormat),
                                (Math.Abs(entry._tangent) + 1).ToString(CultureInfo.InvariantCulture.NumberFormat)));
                        }
                        file.WriteLine(" }");
                        file.WriteLine("}");
                    }
                }
            }
        }

        public static CHR0Node Read(string input)
        {
            CHR0Node node = new CHR0Node() { _name = Path.GetFileNameWithoutExtension(input) };
            using (StreamReader file = new StreamReader(input))
            {
                float start = 0.0f;
                float end = 0.0f;
                string line = "";
                while (true)
                {
                    line = file.ReadLine();
                    int i = line.IndexOf(' ');
                    string tag = line.Substring(0, i);

                    if (tag == "anim")
                        break;

                    string val = line.Substring(i + 1, line.IndexOf(';') - i - 1);

                    switch (tag)
                    {
                        case "startTime":
                        case "startUnitless":
                            float.TryParse(val, out start);
                            break;
                        case "endTime":
                        case "endUnitless":
                            float.TryParse(val, out end);
                            break;

                        case "animVersion":
                        case "mayaVersion":
                        case "timeUnit":
                        case "linearUnit":
                        case "angularUnit":
                        default:
                            break;
                    }
                }

                int frameCount = (int)(end - start + 1.5f);
                node.FrameCount = frameCount;

                while (true)
                {
                    if (line == null)
                        break;

                    string[] anim = line.Split(' ');
                    if (anim.Length != 7)
                    {
                        while (!(line = file.ReadLine()).StartsWith("anim ")) ;
                        continue;
                    }
                    string t = anim[2];
                    string bone = anim[3];
                    KeyFrameMode mode = KeyFrameMode.All;
                    if (t.StartsWith("scale"))
                    {
                        if (t.EndsWith("X"))
                            mode = KeyFrameMode.ScaleX;
                        else if (t.EndsWith("Y"))
                            mode = KeyFrameMode.ScaleY;
                        else if (t.EndsWith("Z"))
                            mode = KeyFrameMode.ScaleZ;
                    }
                    else if (t.StartsWith("rotate"))
                    {
                        if (t.EndsWith("X"))
                            mode = KeyFrameMode.RotX;
                        else if (t.EndsWith("Y"))
                            mode = KeyFrameMode.RotY;
                        else if (t.EndsWith("Z"))
                            mode = KeyFrameMode.RotZ;
                    }
                    else if (t.StartsWith("translate"))
                    {
                        if (t.EndsWith("X"))
                            mode = KeyFrameMode.TransX;
                        else if (t.EndsWith("Y"))
                            mode = KeyFrameMode.TransY;
                        else if (t.EndsWith("Z"))
                            mode = KeyFrameMode.TransZ;
                    }

                    if (mode == KeyFrameMode.All)
                    {
                        while (!(line = file.ReadLine()).StartsWith("anim ")) ;
                        continue;
                    }

                    line = file.ReadLine();

                    if (line.StartsWith("animData"))
                    {
                        CHR0EntryNode e;

                        if ((e = node.FindChild(bone, false) as CHR0EntryNode) == null)
                        {
                            e = new CHR0EntryNode() { _name = bone, _numFrames = frameCount };
                            node.AddChild(e);
                        }

                        while (true)
                        {
                            line = file.ReadLine().TrimStart();
                            int i = line.IndexOf(' ');

                            if (i < 0)
                                break;

                            string tag = line.Substring(0, i);

                            if (tag == "keys")
                            {
                                List<KeyframeEntry> l = new List<KeyframeEntry>();
                                while (true)
                                {
                                    line = file.ReadLine().TrimStart();

                                    if (line == "}")
                                        break;

                                    string[] s = line.Split(' ');

                                    float inVal, outVal;
                                    float.TryParse(s[0], out inVal);
                                    float.TryParse(s[1], out outVal);

                                    float finalTan = 0;

                                    float weight1 = 0;
                                    float weight2 = 0;

                                    float angle1 = 0;
                                    float angle2 = 0;

                                    bool firstFixed = false;
                                    bool secondFixed = false;
                                    switch (s[2])
                                    {
                                        case "linear":
                                        case "spline":
                                            break;

                                        case "fixed":
                                            firstFixed = true;
                                            float.TryParse(s[7], out angle1);
                                            float.TryParse(s[8], out weight1);
                                            break;
                                    }

                                    switch (s[3])
                                    {
                                        case "linear":
                                        case "spline":
                                            break;

                                        case "fixed":
                                            secondFixed = true;
                                            if (firstFixed)
                                            {
                                                float.TryParse(s[9], out angle2);
                                                float.TryParse(s[10], out weight2);
                                            }
                                            else
                                            {
                                                float.TryParse(s[7], out angle2);
                                                float.TryParse(s[8], out weight2);
                                            }
                                            break;
                                    }
                                    bool anyFixed = (secondFixed || firstFixed);
                                    bool bothFixed = (secondFixed && firstFixed);

                                    KeyframeEntry x = e.SetKeyframe(mode, (int)(inVal - 0.5f), outVal, true);
                                    if (!anyFixed)
                                        l.Add(x);
                                    else
                                    {
                                        if (bothFixed)
                                            finalTan = (float)Math.Tan(((angle1 + angle2) / 2) * Maths._deg2radf) * ((weight1 + weight2) / 2);
                                        else if (firstFixed)
                                            finalTan = (float)Math.Tan(angle1 * Maths._deg2radf) * weight1;
                                        else
                                            finalTan = (float)Math.Tan(angle2 * Maths._deg2radf) * weight2;

                                        x._tangent = finalTan;
                                    }
                                }
                                foreach (KeyframeEntry w in l)
                                    w.GenerateTangent();
                            }
                            else
                            {
                                int z = line.IndexOf(';') - i - 1;
                                if (z < 0)
                                    continue;
                                string val = line.Substring(i + 1, z);

                                switch (tag)
                                {
                                    case "input":

                                        break;
                                    case "output":

                                        break;
                                    case "weighted":

                                        break;
                                    case "inputUnit":

                                        break;
                                    case "outputUnit":

                                        break;
                                    case "preInfinity":
                                    case "postInfinity":
                                    default:
                                        break;
                                }
                            }
                        }
                    }

                    line = file.ReadLine();
                }
            }

            return node;
        }
    }
}
