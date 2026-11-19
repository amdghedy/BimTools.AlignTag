using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace BIMTools.TagAlign.Leaders
{
    /// <summary>Tiny reflection helpers so room/space/area tags compile on any Revit version.</summary>
    internal static class Refl
    {
        public static object Get(object o, string name)
        {
            try { PropertyInfo p = o.GetType().GetProperty(name); return p == null ? null : p.GetValue(o, null); }
            catch { return null; }
        }
        public static bool Set(object o, string name, object v)
        {
            try
            {
                PropertyInfo p = o.GetType().GetProperty(name);
                if (p == null || !p.CanWrite) return false;
                p.SetValue(o, v, null); return true;
            }
            catch { return false; }
        }
    }

    /// <summary>One annotation (tag, room/space/area tag or text note) behind a common interface.</summary>
    internal abstract class Annot
    {
        public Element El;
        public string Key = "";
        public virtual bool HasLeader { get { return true; } }
        public abstract void Prepare(LeaderAlignResult r);
        public abstract bool Read(out XYZ head, out XYZ end, out XYZ elbow);
        public abstract void Apply(XYZ head, XYZ end, XYZ elbow, LeaderAlignResult r);
    }

    internal class TagAnnot : Annot
    {
        readonly IndependentTag _t;
        public TagAnnot(IndependentTag t)
        {
            _t = t; El = t;
            string tx = Refl.Get(t, "TagText") as string;
            Key = tx ?? "";
        }
        public override void Prepare(LeaderAlignResult r)
        {
            try
            {
                if (!_t.HasLeader) _t.HasLeader = true;
                if (_t.LeaderEndCondition != LeaderEndCondition.Free &&
                    _t.CanLeaderEndConditionBeAssigned(LeaderEndCondition.Free))
                    _t.LeaderEndCondition = LeaderEndCondition.Free;
            }
            catch (Exception ex) { r.Notes.Add("Leader setup failed on tag " + El.Id + ": " + ex.Message); }
        }
        public override bool Read(out XYZ head, out XYZ end, out XYZ elbow)
        {
            head = null; end = null; elbow = null;
            try
            {
                head = _t.TagHeadPosition;
                TagLeaderHelper.GetLeaderPoints(_t, out end, out elbow);
                return head != null && end != null;
            }
            catch { return false; }
        }
        public override void Apply(XYZ head, XYZ end, XYZ elbow, LeaderAlignResult r)
        {
            _t.TagHeadPosition = head;
            TagLeaderHelper.SetLeaderPoints(_t, end, elbow, r);
        }
    }

    /// <summary>Room / Space / Area tags (accessed by reflection).</summary>
    internal class SpatialAnnot : Annot
    {
        public SpatialAnnot(Element e) { El = e; }
        public override void Prepare(LeaderAlignResult r) { Refl.Set(El, "HasLeader", true); }
        public override bool Read(out XYZ head, out XYZ end, out XYZ elbow)
        {
            head = Refl.Get(El, "TagHeadPosition") as XYZ;
            end = Refl.Get(El, "LeaderEnd") as XYZ;
            elbow = Refl.Get(El, "LeaderElbow") as XYZ;
            return head != null && end != null;
        }
        public override void Apply(XYZ head, XYZ end, XYZ elbow, LeaderAlignResult r)
        {
            Refl.Set(El, "TagHeadPosition", head);
            if (end != null) Refl.Set(El, "LeaderEnd", end);
            if (elbow != null && !Refl.Set(El, "LeaderElbow", elbow)) r.ElbowFailures++;
        }
    }

    internal class NoteAnnot : Annot
    {
        readonly TextNote _n;
        public NoteAnnot(TextNote n)
        {
            _n = n; El = n;
            try { Key = n.Text ?? ""; } catch { }
        }
        public override bool HasLeader { get { return false; } }
        public override void Prepare(LeaderAlignResult r) { }
        public override bool Read(out XYZ head, out XYZ end, out XYZ elbow)
        {
            head = _n.Coord; end = head; elbow = null; return true;
        }
        public override void Apply(XYZ head, XYZ end, XYZ elbow, LeaderAlignResult r) { _n.Coord = head; }
    }

    internal static class AnnotFactory
    {
        public static bool IsSpatial(Element e)
        {
            string n = e.GetType().Name;
            return n == "RoomTag" || n == "SpaceTag" || n == "AreaTag";
        }

        public static bool IsSupported(Element e, bool notes, bool spatial)
        {
            return e is IndependentTag || (notes && e is TextNote) || (spatial && IsSpatial(e));
        }

        public static List<Annot> Build(IEnumerable<Element> els, bool notes, bool spatial)
        {
            List<Annot> list = new List<Annot>();
            foreach (Element e in els)
            {
                if (e == null) continue;
                IndependentTag t = e as IndependentTag;
                TextNote n = e as TextNote;
                if (t != null) list.Add(new TagAnnot(t));
                else if (n != null && notes) list.Add(new NoteAnnot(n));
                else if (spatial && IsSpatial(e)) list.Add(new SpatialAnnot(e));
            }
            return list;
        }

        public static List<Element> AllInView(Document doc, View view, bool notes, bool spatial)
        {
            List<Element> list = new List<Element>();
            list.AddRange(new FilteredElementCollector(doc, view.Id).OfClass(typeof(IndependentTag)).ToElements());
            if (notes) list.AddRange(new FilteredElementCollector(doc, view.Id).OfClass(typeof(TextNote)).ToElements());
            if (spatial)
            {
                List<BuiltInCategory> cats = new List<BuiltInCategory>
                    { BuiltInCategory.OST_RoomTags, BuiltInCategory.OST_MEPSpaceTags, BuiltInCategory.OST_AreaTags };
                list.AddRange(new FilteredElementCollector(doc, view.Id)
                    .WherePasses(new ElementMulticategoryFilter(cats)).WhereElementIsNotElementType().ToElements());
            }
            return list;
        }
    }

    /// <summary>Natural string order: "WP2" before "WP10".</summary>
    internal class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new NaturalComparer();
        public int Compare(string a, string b)
        {
            a = a ?? ""; b = b ?? "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    string x = a.Substring(si, i - si).TrimStart('0'), y = b.Substring(sj, j - sj).TrimStart('0');
                    if (x.Length != y.Length) return x.Length - y.Length;
                    int c = string.CompareOrdinal(x, y);
                    if (c != 0) return c;
                }
                else
                {
                    int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return (a.Length - i) - (b.Length - j);
        }
    }
    internal static class TagLeaderHelper
    {
        public static bool GetLeaderPoints(IndependentTag t, out XYZ end, out XYZ elbow)
        {
            end = null;
            elbow = null;
            try
            {
                PropertyInfo propEnd = t.GetType().GetProperty("LeaderEnd");
                if (propEnd != null)
                {
                    end = propEnd.GetValue(t, null) as XYZ;
                    PropertyInfo propElbow = t.GetType().GetProperty("LeaderElbow");
                    if (propElbow != null)
                    {
                        try { elbow = propElbow.GetValue(t, null) as XYZ; } catch { }
                    }
                    return end != null;
                }

                MethodInfo getRefs = t.GetType().GetMethod("GetTaggedReferences", Type.EmptyTypes);
                MethodInfo getLeaderEnd = t.GetType().GetMethod("GetLeaderEnd", new Type[] { typeof(Reference) });
                MethodInfo getLeaderElbow = t.GetType().GetMethod("GetLeaderElbow", new Type[] { typeof(Reference) });

                if (getRefs != null && getLeaderEnd != null)
                {
                    var refs = getRefs.Invoke(t, null) as System.Collections.IEnumerable;
                    if (refs != null)
                    {
                        foreach (Reference r in refs)
                        {
                            end = getLeaderEnd.Invoke(t, new object[] { r }) as XYZ;
                            if (getLeaderElbow != null)
                            {
                                try { elbow = getLeaderElbow.Invoke(t, new object[] { r }) as XYZ; } catch { }
                            }
                            if (end != null) return true;
                        }
                    }
                }
            }
            catch { }
            return end != null;
        }

        public static void SetLeaderPoints(IndependentTag t, XYZ end, XYZ elbow, LeaderAlignResult r)
        {
            try
            {
                PropertyInfo propEnd = t.GetType().GetProperty("LeaderEnd");
                if (propEnd != null && propEnd.CanWrite)
                {
                    if (end != null) { try { propEnd.SetValue(t, end, null); } catch { } }
                    PropertyInfo propElbow = t.GetType().GetProperty("LeaderElbow");
                    if (propElbow != null && propElbow.CanWrite && elbow != null)
                    {
                        try { propElbow.SetValue(t, elbow, null); } catch { r.ElbowFailures++; }
                    }
                    return;
                }

                MethodInfo getRefs = t.GetType().GetMethod("GetTaggedReferences", Type.EmptyTypes);
                MethodInfo setLeaderEnd = t.GetType().GetMethod("SetLeaderEnd", new Type[] { typeof(Reference), typeof(XYZ) });
                MethodInfo setLeaderElbow = t.GetType().GetMethod("SetLeaderElbow", new Type[] { typeof(Reference), typeof(XYZ) });

                if (getRefs != null)
                {
                    var refs = getRefs.Invoke(t, null) as System.Collections.IEnumerable;
                    if (refs != null)
                    {
                        foreach (Reference refItem in refs)
                        {
                            if (end != null && setLeaderEnd != null)
                            {
                                try { setLeaderEnd.Invoke(t, new object[] { refItem, end }); } catch { }
                            }
                            if (elbow != null && setLeaderElbow != null)
                            {
                                try { setLeaderElbow.Invoke(t, new object[] { refItem, elbow }); }
                                catch { r.ElbowFailures++; }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                r.Notes.Add("Failed setting leader points: " + ex.Message);
            }
        }
    }
}

