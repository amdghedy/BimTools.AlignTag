using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIMTools.TagAlign.Leaders
{
    public class AuditCategory
    {
        public string Name;
        public int Count;
        public override string ToString() { return Name + "  (" + Count + ")"; }
    }

    public class AuditResult
    {
        public int TagCount, ElementCount;
        public List<ElementId> Untagged = new List<ElementId>();
        public List<ElementId> Duplicates = new List<ElementId>();     // the extra tags (one tag per element and tag type is kept)
        public List<ElementId> Orphans = new List<ElementId>();
        public Dictionary<string, int> UntaggedByCategory = new Dictionary<string, int>();
    }

    /// <summary>
    /// "Tag Audit": checks one view for model elements that have no tag, elements that carry the same tag
    /// twice, and tags that lost their element. Revit-version safe (the tag API changed in 2022).
    /// </summary>
    public static class TagAudit
    {
        /// <summary>Taggable model categories that really occur in the view, with element counts.</summary>
        public static List<AuditCategory> Categories(Document doc, View view)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (Element e in new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType())
            {
                Category c = e.Category;
                if (c == null || c.CategoryType != CategoryType.Model) continue;
                object tg = Refl.Get(c, "IsTaggable");
                if (tg is bool && !(bool)tg) continue;
                int n;
                counts.TryGetValue(c.Name, out n);
                counts[c.Name] = n + 1;
            }
            return counts.Select(kv => new AuditCategory { Name = kv.Key, Count = kv.Value })
                         .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Elements a tag is attached to (works with the old single-host and the new multi-host API).</summary>
        static List<ElementId> HostIds(IndependentTag t)
        {
            List<ElementId> l = new List<ElementId>();
            try
            {
                System.Reflection.MethodInfo m = t.GetType().GetMethod("GetTaggedLocalElementIds", Type.EmptyTypes);
                if (m != null)
                {
                    IEnumerable<ElementId> ids = m.Invoke(t, null) as IEnumerable<ElementId>;
                    if (ids != null) { l.AddRange(ids.Where(i => i != ElementId.InvalidElementId)); return l; }
                }
            }
            catch { }
            try
            {
                ElementId id = Refl.Get(t, "TaggedLocalElementId") as ElementId;
                if (id != null && id != ElementId.InvalidElementId) l.Add(id);
            }
            catch { }
            return l;
        }

        static bool PointsToLink(IndependentTag t)
        {
            try
            {
                object le = Refl.Get(t, "TaggedElementId");               // LinkElementId in old API
                if (le == null) return false;
                ElementId linked = Refl.Get(le, "LinkedElementId") as ElementId;
                return linked != null && linked != ElementId.InvalidElementId;
            }
            catch { return false; }
        }

        public static AuditResult Run(Document doc, View view, ICollection<string> categories, bool untagged, bool duplicates, bool orphans)
        {
            AuditResult r = new AuditResult();
            List<IndependentTag> tags = new FilteredElementCollector(doc, view.Id).OfClass(typeof(IndependentTag))
                                            .Cast<IndependentTag>().OrderBy(t => t.Id.ToString()).ToList();
            r.TagCount = tags.Count;

            HashSet<ElementId> tagged = new HashSet<ElementId>();
            Dictionary<string, List<IndependentTag>> perHost = new Dictionary<string, List<IndependentTag>>();
            foreach (IndependentTag t in tags)
            {
                List<ElementId> hosts = HostIds(t);
                foreach (ElementId h in hosts)
                {
                    tagged.Add(h);
                    string key = h + "|" + t.GetTypeId();
                    List<IndependentTag> l;
                    if (!perHost.TryGetValue(key, out l)) { l = new List<IndependentTag>(); perHost[key] = l; }
                    l.Add(t);
                }
                if (orphans && hosts.Count == 0 && !PointsToLink(t))
                {
                    object orph = Refl.Get(t, "IsOrphaned");
                    string text = Refl.Get(t, "TagText") as string;
                    bool empty = string.IsNullOrWhiteSpace(text) || text.Trim() == "?";
                    if ((orph is bool && (bool)orph) || empty) r.Orphans.Add(t.Id);
                }
            }

            if (duplicates)
            {
                HashSet<ElementId> seen = new HashSet<ElementId>();
                foreach (List<IndependentTag> l in perHost.Values.Where(x => x.Count > 1))
                    foreach (IndependentTag t in l.Skip(1))                 // keep the first (lowest id), flag the rest
                        if (seen.Add(t.Id)) r.Duplicates.Add(t.Id);
            }

            if (untagged && categories != null && categories.Count > 0)
            {
                foreach (Element e in new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType())
                {
                    Category c = e.Category;
                    if (c == null || c.CategoryType != CategoryType.Model || !categories.Contains(c.Name)) continue;
                    r.ElementCount++;
                    if (tagged.Contains(e.Id)) continue;
                    r.Untagged.Add(e.Id);
                    int n;
                    r.UntaggedByCategory.TryGetValue(c.Name, out n);
                    r.UntaggedByCategory[c.Name] = n + 1;
                }
            }
            return r;
        }
    }
}
