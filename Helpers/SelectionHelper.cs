using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AlignTool.Helpers
{
    public static class SelectionHelper
    {
        public static List<IndependentTag> GetSelectedTags(UIDocument uidoc, bool allowPick = true, string prompt = "Select tags")
        {
            var result = new List<IndependentTag>();
            if (uidoc == null) return result;

            var selectedIds = uidoc.Selection.GetElementIds();
            Document doc = uidoc.Document;
            foreach (var id in selectedIds)
            {
                if (doc.GetElement(id) is IndependentTag tag)
                    result.Add(tag);
            }

            if (result.Count == 0 && allowPick)
            {
                try
                {
                    var refs = uidoc.Selection.PickObjects(ObjectType.Element, new TagSelectionFilter(), prompt);
                    foreach (var r in refs)
                    {
                        if (doc.GetElement(r) is IndependentTag tag)
                            result.Add(tag);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            }
            return result;
        }

        public static IList<IndependentTag> GetAllTagsInView(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();
        }

        private class TagSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element e) => e is IndependentTag;
            public bool AllowReference(Reference r, XYZ p) => true;
        }
    }
}
