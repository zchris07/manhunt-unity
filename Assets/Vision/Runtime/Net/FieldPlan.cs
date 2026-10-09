using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Vision.Net
{
    /// <summary>
    /// How to put an object's plain fields on the wire: every instance field of a supported type (numbers, flags, enums,
    /// strings, vectors, arrays of those, int-to-float maps and int sets), in a fixed order, read and written by reflection
    /// (cached per type). References to other objects are left out: the snapshot sends those separately or not at all.
    /// </summary>
    public sealed class FieldPlan
    {
        enum Kind : byte { Bool, Byte, Int, UInt, Float, String, Enum, Vector2, NullableVector2, IntArray, FloatArray, BoolArray, Vector2Array, EnumArray, IntFloatMap, IntSet }

        readonly FieldInfo[] fields;
        readonly Kind[] kinds;
        public int Count => fields.Length;
        public string NameOf(int i) => fields[i].Name;

        static readonly Dictionary<(Type, bool), FieldPlan> cache = new Dictionary<(Type, bool), FieldPlan>();

        /// <summary>The plan for a type: public fields, or with <paramref name="all"/> its private and inherited ones too.</summary>
        public static FieldPlan For(Type t, bool all = false, params string[] exclude)
        {
            if (cache.TryGetValue((t, all), out FieldPlan p)) return p;
            p = new FieldPlan(t, all, exclude);
            cache[(t, all)] = p;
            return p;
        }

        FieldPlan(Type t, bool all, string[] exclude)
        {
            var list = new List<FieldInfo>();
            var kindList = new List<Kind>();
            var seen = new HashSet<string>();
            // Base classes first, so a subclass shares its base's field order.
            var chain = new List<Type>();
            for (Type c = t; c != null && c != typeof(object); c = all ? c.BaseType : null) chain.Insert(0, c);
            foreach (Type c in chain)
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly | (all ? BindingFlags.NonPublic : 0);
                var declared = c.GetFields(flags);
                Array.Sort(declared, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
                foreach (FieldInfo f in declared)
                {
                    if (Array.IndexOf(exclude, f.Name) >= 0 || f.Name.Contains("k__BackingField") || !seen.Add(c.Name + "." + f.Name)) continue;
                    if (!KindOf(f.FieldType, out Kind k)) continue;
                    list.Add(f);
                    kindList.Add(k);
                }
            }
            fields = list.ToArray();
            kinds = kindList.ToArray();
        }

        static bool KindOf(Type t, out Kind k)
        {
            k = Kind.Int;
            if (t == typeof(bool)) k = Kind.Bool;
            else if (t == typeof(byte)) k = Kind.Byte;
            else if (t == typeof(int)) k = Kind.Int;
            else if (t == typeof(uint)) k = Kind.UInt;
            else if (t == typeof(float)) k = Kind.Float;
            else if (t == typeof(string)) k = Kind.String;
            else if (t.IsEnum) k = Kind.Enum;
            else if (t == typeof(Vector2)) k = Kind.Vector2;
            else if (t == typeof(Vector2?)) k = Kind.NullableVector2;
            else if (t == typeof(int[])) k = Kind.IntArray;
            else if (t == typeof(float[])) k = Kind.FloatArray;
            else if (t == typeof(bool[])) k = Kind.BoolArray;
            else if (t == typeof(Vector2[])) k = Kind.Vector2Array;
            else if (t.IsArray && t.GetElementType().IsEnum) k = Kind.EnumArray;
            else if (t == typeof(Dictionary<int, float>)) k = Kind.IntFloatMap;
            else if (t == typeof(HashSet<int>)) k = Kind.IntSet;
            else return false;
            return true;
        }

        /// <summary>Writes every field, noting where each one starts (offsets has Count + 1 entries).</summary>
        public void WriteAll(BinaryWriter w, object o, int[] offsets)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                offsets[i] = (int)w.BaseStream.Position;
                Write(w, o, i);
            }
            offsets[fields.Length] = (int)w.BaseStream.Position;
        }

        public void Write(BinaryWriter w, object o, int i)
        {
            object v = fields[i].GetValue(o);
            switch (kinds[i])
            {
                case Kind.Bool: w.Write((bool)v); break;
                case Kind.Byte: w.Write((byte)v); break;
                case Kind.Int: w.Write((int)v); break;
                case Kind.UInt: w.Write((uint)v); break;
                case Kind.Float: w.Write((float)v); break;
                case Kind.String: Wire.WriteString(w, (string)v); break;
                case Kind.Enum: w.Write(Convert.ToInt32(v)); break;
                case Kind.Vector2: Wire.Write(w, (Vector2)v); break;
                case Kind.NullableVector2:
                {
                    var n = (Vector2?)v;
                    w.Write(n.HasValue);
                    if (n.HasValue) Wire.Write(w, n.Value);
                    break;
                }
                case Kind.IntArray:
                {
                    var a = (int[])v;
                    w.Write(a?.Length ?? -1);
                    if (a != null) foreach (int x in a) w.Write(x);
                    break;
                }
                case Kind.FloatArray:
                {
                    var a = (float[])v;
                    w.Write(a?.Length ?? -1);
                    if (a != null) foreach (float x in a) w.Write(x);
                    break;
                }
                case Kind.BoolArray:
                {
                    var a = (bool[])v;
                    w.Write(a?.Length ?? -1);
                    if (a != null) foreach (bool x in a) w.Write(x);
                    break;
                }
                case Kind.Vector2Array:
                {
                    var a = (Vector2[])v;
                    w.Write(a?.Length ?? -1);
                    if (a != null) foreach (Vector2 x in a) Wire.Write(w, x);
                    break;
                }
                case Kind.EnumArray:
                {
                    var a = (Array)v;
                    w.Write(a?.Length ?? -1);
                    if (a != null) foreach (object x in a) w.Write(Convert.ToInt32(x));
                    break;
                }
                case Kind.IntFloatMap:
                {
                    var m = (Dictionary<int, float>)v;
                    w.Write(m?.Count ?? 0);
                    if (m != null)
                        foreach (var kv in m)
                        {
                            w.Write(kv.Key);
                            w.Write(kv.Value);
                        }
                    break;
                }
                case Kind.IntSet:
                {
                    var s = (HashSet<int>)v;
                    w.Write(s?.Count ?? 0);
                    if (s != null) foreach (int x in s) w.Write(x);
                    break;
                }
            }
        }

        /// <summary>Reads one field into the object (arrays, maps and sets are filled in place when the object owns one).</summary>
        public void Read(BinaryReader r, object o, int i)
        {
            FieldInfo f = fields[i];
            switch (kinds[i])
            {
                case Kind.Bool: f.SetValue(o, r.ReadBoolean()); break;
                case Kind.Byte: f.SetValue(o, r.ReadByte()); break;
                case Kind.Int: f.SetValue(o, r.ReadInt32()); break;
                case Kind.UInt: f.SetValue(o, r.ReadUInt32()); break;
                case Kind.Float: f.SetValue(o, r.ReadSingle()); break;
                case Kind.String: f.SetValue(o, Wire.ReadString(r)); break;
                case Kind.Enum: f.SetValue(o, Enum.ToObject(f.FieldType, r.ReadInt32())); break;
                case Kind.Vector2: f.SetValue(o, Wire.ReadVector2(r)); break;
                case Kind.NullableVector2: f.SetValue(o, r.ReadBoolean() ? Wire.ReadVector2(r) : (Vector2?)null); break;
                case Kind.IntArray:
                {
                    int n = r.ReadInt32();
                    int[] a = n < 0 ? null : Fit((int[])f.GetValue(o), n);
                    for (int k = 0; k < n; k++) a[k] = r.ReadInt32();
                    f.SetValue(o, a);
                    break;
                }
                case Kind.FloatArray:
                {
                    int n = r.ReadInt32();
                    float[] a = n < 0 ? null : Fit((float[])f.GetValue(o), n);
                    for (int k = 0; k < n; k++) a[k] = r.ReadSingle();
                    f.SetValue(o, a);
                    break;
                }
                case Kind.BoolArray:
                {
                    int n = r.ReadInt32();
                    bool[] a = n < 0 ? null : Fit((bool[])f.GetValue(o), n);
                    for (int k = 0; k < n; k++) a[k] = r.ReadBoolean();
                    f.SetValue(o, a);
                    break;
                }
                case Kind.Vector2Array:
                {
                    int n = r.ReadInt32();
                    Vector2[] a = n < 0 ? null : Fit((Vector2[])f.GetValue(o), n);
                    for (int k = 0; k < n; k++) a[k] = Wire.ReadVector2(r);
                    f.SetValue(o, a);
                    break;
                }
                case Kind.EnumArray:
                {
                    int n = r.ReadInt32();
                    Type et = f.FieldType.GetElementType();
                    var a = (Array)f.GetValue(o);
                    if (n >= 0 && (a == null || a.Length != n)) a = Array.CreateInstance(et, n);
                    for (int k = 0; k < n; k++) a.SetValue(Enum.ToObject(et, r.ReadInt32()), k);
                    f.SetValue(o, n < 0 ? null : a);
                    break;
                }
                case Kind.IntFloatMap:
                {
                    int n = r.ReadInt32();
                    var m = (Dictionary<int, float>)f.GetValue(o);
                    if (m == null) { m = new Dictionary<int, float>(); f.SetValue(o, m); }
                    m.Clear();
                    for (int k = 0; k < n; k++) m[r.ReadInt32()] = r.ReadSingle();
                    break;
                }
                case Kind.IntSet:
                {
                    int n = r.ReadInt32();
                    var s = (HashSet<int>)f.GetValue(o);
                    if (s == null) { s = new HashSet<int>(); f.SetValue(o, s); }
                    s.Clear();
                    for (int k = 0; k < n; k++) s.Add(r.ReadInt32());
                    break;
                }
            }
        }

        static T[] Fit<T>(T[] a, int n) => a != null && a.Length == n ? a : new T[n];

        public void ReadAll(BinaryReader r, object o)
        {
            for (int i = 0; i < fields.Length; i++) Read(r, o, i);
        }

        /// <summary>Writes a list of objects of one type (a count, then each one's fields).</summary>
        public static void WriteList(BinaryWriter w, IList list, Type element)
        {
            FieldPlan plan = For(element);
            w.Write(list.Count);
            foreach (object o in list) plan.WriteAll(w, o, new int[plan.Count + 1]);
        }

        /// <summary>Reads a list written by <see cref="WriteList"/>, replacing its contents (new elements, structs boxed in turn).</summary>
        public static void ReadList(BinaryReader r, IList list, Type element)
        {
            FieldPlan plan = For(element);
            int n = r.ReadInt32();
            list.Clear();
            for (int k = 0; k < n; k++)
            {
                object o = Activator.CreateInstance(element);
                plan.ReadAll(r, o);
                list.Add(o);
            }
        }
    }
}
