using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AlignTool.Services
{
    /// <summary>
    /// Class-agnostic alignment for Revit annotation tags exposing a writable XYZ
    /// TagHeadPosition (IndependentTag, RoomTag, AreaTag, SpaceTag and compatible
    /// API/family tag classes). Coordinates are measured in the active view plane.
    /// </summary>
    public static class UniversalAnnotationService
    {
        public enum AlignMode { Left, Center, Right, Top, Middle, Bottom }

        private static readonly string[] SupportedTagTypeNames =
        {
            "IndependentTag", "RoomTag", "AreaTag", "SpaceTag"
        };

        public static bool Supports(Element element)
        {
            if (element == null || element is ElementType) return false;
            PropertyInfo property = element.GetType().GetProperty(
                "TagHeadPosition", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || !property.CanWrite ||
                property.PropertyType != typeof(XYZ)) return false;

            // Do not accidentally treat arbitrary custom elements as tags.
            Type type = element.GetType();
            while (type != null)
            {
                if (SupportedTagTypeNames.Contains(type.Name)) return true;
                type = type.BaseType;
            }

            // Some Revit releases expose a tag subtype through a proxy type.
            return element.Category != null &&
                   element.Category.Name.IndexOf("tag", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static IList<Element> GetSelectedOrPick(UIDocument uidoc, string prompt)
        {
            var result = new List<Element>();
            if (uidoc == null) return result;
            Document doc = uidoc.Document;

            foreach (ElementId id in uidoc.Selection.GetElementIds())
            {
                Element element = doc.GetElement(id);
                if (Supports(element)) result.Add(element);
            }

            if (result.Count == 0)
            {
                try
                {
                    IList<Reference> picked = uidoc.Selection.PickObjects(
                        ObjectType.Element, new UniversalTagSelectionFilter(), prompt);
                    foreach (Reference reference in picked)
                    {
                        Element element = doc.GetElement(reference);
                        if (Supports(element) && !result.Any(x => x.Id == element.Id))
                            result.Add(element);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            }
            return result;
        }

        public static IList<Element> GetAllInView(Document doc, View view)
        {
            if (doc == null || view == null) return new List<Element>();
            return new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .Where(Supports)
                .ToList();
        }

        public static int Align(Document doc, View view, IList<Element> elements, AlignMode mode)
        {
            if (doc == null || view == null || elements == null || elements.Count < 2) return 0;
            XYZ axis = IsHorizontal(mode) ? view.RightDirection : view.UpDirection;
            axis = axis.Normalize();
            var movable = elements.Where(e => e != null && !e.Pinned && Supports(e)).ToList();
            if (movable.Count < 2) return 0;

            var coordinates = new List<Coordinate>();
            foreach (Element element in movable)
            {
                XYZ position = GetHead(element);
                if (position != null)
                    coordinates.Add(new Coordinate(element, position, position.DotProduct(axis)));
            }
            if (coordinates.Count < 2) return 0;

            double min = coordinates.Min(x => x.Value);
            double max = coordinates.Max(x => x.Value);
            double target;
            switch (mode)
            {
                case AlignMode.Left:
                case AlignMode.Bottom: target = min; break;
                case AlignMode.Right:
                case AlignMode.Top: target = max; break;
                case AlignMode.Center:
                case AlignMode.Middle: target = (min + max) * 0.5; break;
                default: return 0;
            }

            return ApplyMoves(doc, coordinates.Select(x =>
                new Move(x.Element, x.Position + axis * (target - x.Value))));
        }

        public static int Distribute(Document doc, View view, IList<Element> elements, bool horizontal)
        {
            if (doc == null || view == null || elements == null || elements.Count < 3) return 0;
            XYZ axis = (horizontal ? view.RightDirection : view.UpDirection).Normalize();
            var ordered = elements.Where(e => e != null && !e.Pinned && Supports(e))
                .Select(e => new { Element = e, Position = GetHead(e) })
                .Where(x => x.Position != null)
                .Select(x => new { x.Element, x.Position, Value = x.Position.DotProduct(axis) })
                .OrderBy(x => x.Value).ToList();
            if (ordered.Count < 3) return 0;

            double first = ordered[0].Value;
            double last = ordered[ordered.Count - 1].Value;
            if (Math.Abs(last - first) < 1e-9) return 0;
            double step = (last - first) / (ordered.Count - 1);

            var moves = new List<Move>();
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                var item = ordered[i];
                XYZ destination = item.Position + axis * (first + i * step - item.Value);
                moves.Add(new Move(item.Element, destination));
            }
            return ApplyMoves(doc, moves);
        }

        private sealed class Coordinate
        {
            public Element Element { get; private set; }
            public XYZ Position { get; private set; }
            public double Value { get; private set; }
            public Coordinate(Element element, XYZ position, double value)
            { Element = element; Position = position; Value = value; }
        }

        private sealed class Move
        {
            public Element Element { get; private set; }
            public XYZ Destination { get; private set; }
            public Move(Element element, XYZ destination) { Element = element; Destination = destination; }
        }

        private static int ApplyMoves(Document doc, IEnumerable<Move> moves)
        {
            int moved = 0;
            using (var transaction = new Transaction(doc, "Universal Annotation Alignment"))
            {
                transaction.Start();
                foreach (Move move in moves)
                {
                    using (var sub = new SubTransaction(doc))
                    {
                        try
                        {
                            sub.Start();
                            PropertyInfo property = move.Element.GetType().GetProperty(
                                "TagHeadPosition", BindingFlags.Instance | BindingFlags.Public);
                            if (property == null || !property.CanWrite)
                                throw new InvalidOperationException("Tag head position is not writable.");
                            property.SetValue(move.Element, move.Destination, null);
                            if (sub.Commit() == TransactionStatus.Committed)
                                moved++;
                        }
                        catch
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                        }
                    }
                }
                transaction.Commit();
            }
            return moved;
        }

        private static XYZ GetHead(Element element)
        {
            try
            {
                PropertyInfo property = element.GetType().GetProperty(
                    "TagHeadPosition", BindingFlags.Instance | BindingFlags.Public);
                return property == null ? null : property.GetValue(element, null) as XYZ;
            }
            catch { return null; }
        }

        private static bool IsHorizontal(AlignMode mode)
        {
            return mode == AlignMode.Left || mode == AlignMode.Center || mode == AlignMode.Right;
        }

        private sealed class UniversalTagSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) { return Supports(element); }
            public bool AllowReference(Reference reference, XYZ position) { return true; }
        }
    }
}

namespace AlignTool.Commands
{
    using AlignTool.Services;

    internal static class UniversalAlignRunner
    {
        public static Result Run(ExternalCommandData data, ref string message,
            UniversalAnnotationService.AlignMode? mode, bool? distribute, string title)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc == null ? null : uidoc.Document;
            View view = doc == null ? null : doc.ActiveView;
            if (doc == null || view == null) { message = "No active document or view."; return Result.Failed; }

            try
            {
                IList<Element> tags = UniversalAnnotationService.GetSelectedOrPick(uidoc, "Select annotation tags");
                if (tags.Count < 2)
                {
                    TaskDialog.Show(title, "Select at least two supported annotation tags.");
                    return Result.Cancelled;
                }

                int moved = mode.HasValue
                    ? UniversalAnnotationService.Align(doc, view, tags, mode.Value)
                    : UniversalAnnotationService.Distribute(doc, view, tags, distribute == true);
                TaskDialog.Show(title, moved + " annotation tag(s) moved. Pinned tags were skipped.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                AlignTool.Helpers.Logger.Error(title + " failed", ex);
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class UniversalAlignLeftCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, UniversalAnnotationService.AlignMode.Left, null, "Universal Align Left"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalAlignCenterCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, UniversalAnnotationService.AlignMode.Center, null, "Universal Align Center"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalAlignRightCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, UniversalAnnotationService.AlignMode.Right, null, "Universal Align Right"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalAlignTopCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, UniversalAnnotationService.AlignMode.Top, null, "Universal Align Top"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalAlignMiddleCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, UniversalAnnotationService.AlignMode.Middle, null, "Universal Align Middle"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalAlignBottomCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, UniversalAnnotationService.AlignMode.Bottom, null, "Universal Align Bottom"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalDistributeHorizontalCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, null, true, "Universal Distribute Horizontal"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalDistributeVerticalCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => UniversalAlignRunner.Run(c, ref m, null, false, "Universal Distribute Vertical"); }

    [Transaction(TransactionMode.Manual)]
    public class UniversalSelectAllTagsInViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
        {
            UIDocument uidoc = c.Application.ActiveUIDocument;
            Document doc = uidoc == null ? null : uidoc.Document;
            if (doc == null) { m = "No active document."; return Result.Failed; }
            try
            {
                IList<Element> tags = UniversalAnnotationService.GetAllInView(doc, doc.ActiveView);
                uidoc.Selection.SetElementIds(tags.Select(x => x.Id).ToList());
                TaskDialog.Show("Universal Tags", tags.Count + " supported annotation tag(s) selected in the active view.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                m = ex.Message;
                AlignTool.Helpers.Logger.Error("Select all universal tags failed", ex);
                return Result.Failed;
            }
        }
    }
}
