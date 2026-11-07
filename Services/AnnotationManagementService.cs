using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AlignTool.Services
{
    /// <summary>
    /// Geometry-based management for Revit text notes, dimensions and supported tags.
    /// All calculations are performed in the active view coordinate system.
    /// </summary>
    public static class AnnotationManagementService
    {
        private const double DefaultGapMm = 2.0;

        public sealed class Item
        {
            public Element Element;
            public XYZ Center;
            public double Left, Right, Bottom, Top;
            public string Kind;
            public bool Pinned;
        }

        public sealed class MovePlan
        {
            public Element Element;
            public XYZ Delta;
            public string Reason;
        }

        public sealed class AuditResult
        {
            public IList<Item> Items;
            public IList<Tuple<Item, Item>> Collisions;
            public IList<string> Unsupported;
        }

        public static bool Supports(Element element)
        {
            if (element == null || element is ElementType) return false;
            if (UniversalAnnotationService.Supports(element)) return true;
            Type t = element.GetType();
            while (t != null)
            {
                if (t == typeof(TextNote) || t == typeof(Dimension)) return true;
                t = t.BaseType;
            }
            return false;
        }

        public static IList<Element> GetSelectionOrPick(UIDocument uidoc)
        {
            var elements = new List<Element>();
            if (uidoc == null) return elements;
            Document doc = uidoc.Document;
            foreach (ElementId id in uidoc.Selection.GetElementIds())
            {
                Element e = doc.GetElement(id);
                if (Supports(e) && !elements.Any(x => x.Id == e.Id)) elements.Add(e);
            }
            if (elements.Count > 0) return elements;
            try
            {
                IList<Reference> picked = uidoc.Selection.PickObjects(
                    ObjectType.Element, new AnnotationFilter(), "Select text notes, dimensions and annotation tags");
                foreach (Reference r in picked)
                {
                    Element e = doc.GetElement(r);
                    if (Supports(e) && !elements.Any(x => x.Id == e.Id)) elements.Add(e);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            return elements;
        }

        public static AuditResult Audit(Document doc, View view, IList<Element> elements)
        {
            var items = new List<Item>();
            var unsupported = new List<string>();
            foreach (Element e in elements ?? new List<Element>())
            {
                Item item = CreateItem(e, view);
                if (item != null) items.Add(item);
                else if (e != null) unsupported.Add(e.Id.IntegerValue + " (" + e.GetType().Name + "): no usable view bounding box");
            }

            var collisions = new List<Tuple<Item, Item>>();
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                    if (Overlaps(items[i], items[j], MmToFeet(DefaultGapMm)))
                        collisions.Add(Tuple.Create(items[i], items[j]));

            return new AuditResult { Items = items, Collisions = collisions, Unsupported = unsupported };
        }

        public static IList<MovePlan> PlanAlign(IList<Item> items, View view, bool horizontal)
        {
            var plan = new List<MovePlan>();
            if (items == null || items.Count < 2) return plan;
            double target = horizontal
                ? items.Average(x => x.Center.DotProduct(view.UpDirection))
                : items.Average(x => x.Center.DotProduct(view.RightDirection));
            foreach (Item item in items.Where(x => !x.Pinned))
            {
                XYZ axis = horizontal ? view.UpDirection : view.RightDirection;
                double current = item.Center.DotProduct(axis);
                double delta = target - current;
                if (Math.Abs(delta) > 1e-8)
                    plan.Add(new MovePlan { Element = item.Element, Delta = axis * delta, Reason = horizontal ? "Align horizontal centre" : "Align vertical centre" });
            }
            return plan;
        }

        public static IList<MovePlan> PlanDistribute(IList<Item> items, View view, bool horizontal)
        {
            var plan = new List<MovePlan>();
            if (items == null || items.Count < 3) return plan;
            XYZ axis = horizontal ? view.RightDirection : view.UpDirection;
            var ordered = items.Where(x => !x.Pinned)
                .OrderBy(x => x.Center.DotProduct(axis)).ToList();
            if (ordered.Count < 3) return plan;
            double first = ordered.First().Center.DotProduct(axis);
            double last = ordered.Last().Center.DotProduct(axis);
            double step = (last - first) / (ordered.Count - 1);
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                double current = ordered[i].Center.DotProduct(axis);
                double delta = first + i * step - current;
                if (Math.Abs(delta) > 1e-8)
                    plan.Add(new MovePlan { Element = ordered[i].Element, Delta = axis * delta, Reason = "Even distribution" });
            }
            return plan;
        }

        public static IList<MovePlan> PlanCollisionResolution(AuditResult audit, View view, double gapMm)
        {
            var plan = new List<MovePlan>();
            if (audit == null || audit.Items == null) return plan;
            double gap = MmToFeet(Math.Max(0, gapMm));
            var working = audit.Items.Select(Clone).ToList();
            // Stable greedy layout: preserve the first/top-most annotation and move
            // subsequent overlapping items down in view space. No element is changed here.
            working = working.OrderByDescending(x => x.Center.DotProduct(view.UpDirection))
                .ThenBy(x => x.Center.DotProduct(view.RightDirection)).ToList();

            for (int i = 0; i < working.Count; i++)
            {
                Item current = working[i];
                if (current.Pinned) continue;
                double totalShift = 0;
                int attempts = 0;
                bool collision;
                do
                {
                    collision = false;
                    for (int j = 0; j < i; j++)
                    {
                        if (Overlaps(current, working[j], gap))
                        {
                            double shift = (working[j].Bottom - gap) - current.Top;
                            if (shift >= -1e-8) shift = -Math.Max(gap, MmToFeet(2));
                            Shift(current, view.UpDirection * shift, view);
                            totalShift += shift;
                            collision = true;
                            break;
                        }
                    }
                } while (collision && ++attempts < 20);

                if (Math.Abs(totalShift) > 1e-8)
                    plan.Add(new MovePlan { Element = current.Element, Delta = view.UpDirection * totalShift, Reason = "Resolve annotation overlap" });
            }
            return plan;
        }

        public static int Apply(Document doc, IList<MovePlan> plan)
        {
            if (doc == null || plan == null || plan.Count == 0) return 0;
            int moved = 0;
            using (Transaction tx = new Transaction(doc, "BIM TOOLS - Annotation Management"))
            {
                tx.Start();
                foreach (MovePlan move in plan)
                {
                    if (move == null || move.Element == null || move.Element.Pinned) continue;
                    using (SubTransaction sub = new SubTransaction(doc))
                    {
                        try
                        {
                            sub.Start();
                            ElementTransformUtils.MoveElement(doc, move.Element.Id, move.Delta);
                            if (sub.Commit() == TransactionStatus.Committed) moved++;
                        }
                        catch
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                        }
                    }
                }
                tx.Commit();
            }
            return moved;
        }

        public static string Describe(AuditResult audit)
        {
            if (audit == null) return "No audit data.";
            return "Annotations inspected: " + audit.Items.Count +
                   "\nPotential overlaps: " + audit.Collisions.Count +
                   "\nItems without usable view bounds: " + audit.Unsupported.Count +
                   "\nPinned items: " + audit.Items.Count(x => x.Pinned);
        }

        public static string Preview(IList<MovePlan> plan)
        {
            if (plan == null || plan.Count == 0) return "No changes are required.";
            return "Proposed element moves: " + plan.Count +
                   "\nPinned elements are excluded. Review the model after applying; leader and dimension constraints remain governed by Revit.";
        }

        private static Item CreateItem(Element e, View view)
        {
            if (!Supports(e)) return null;
            BoundingBoxXYZ box;
            try { box = e.get_BoundingBox(view); }
            catch { return null; }
            if (box == null) return null;

            XYZ[] corners =
            {
                new XYZ(box.Min.X, box.Min.Y, box.Min.Z), new XYZ(box.Min.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Min.X, box.Max.Y, box.Min.Z), new XYZ(box.Min.X, box.Max.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Min.Y, box.Min.Z), new XYZ(box.Max.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Max.Y, box.Min.Z), new XYZ(box.Max.X, box.Max.Y, box.Max.Z)
            };
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (XYZ raw in corners)
            {
                XYZ p = box.Transform == null ? raw : box.Transform.OfPoint(raw);
                double x = p.DotProduct(view.RightDirection);
                double y = p.DotProduct(view.UpDirection);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            XYZ center = view.RightDirection * ((minX + maxX) * 0.5) +
                         view.UpDirection * ((minY + maxY) * 0.5);
            return new Item
            {
                Element = e, Center = center, Left = minX, Right = maxX,
                Bottom = minY, Top = maxY, Kind = e.GetType().Name, Pinned = e.Pinned
            };
        }

        private static Item Clone(Item x)
        {
            return new Item { Element = x.Element, Center = x.Center, Left = x.Left, Right = x.Right,
                Bottom = x.Bottom, Top = x.Top, Kind = x.Kind, Pinned = x.Pinned };
        }

        private static void Shift(Item item, XYZ delta, View view)
        {
            // Rectangles are stored in view-axis coordinates, not world X/Y.
            double dx = delta.DotProduct(view.RightDirection);
            double dy = delta.DotProduct(view.UpDirection);
            item.Left += dx; item.Right += dx;
            item.Bottom += dy; item.Top += dy;
            item.Center += delta;
        }

        private static bool Overlaps(Item a, Item b, double gap)
        {
            return a.Left < b.Right + gap && a.Right > b.Left - gap &&
                   a.Bottom < b.Top + gap && a.Top > b.Bottom - gap;
        }

        private static double MmToFeet(double mm) { return mm / 304.8; }

        private sealed class AnnotationFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) { return Supports(element); }
            public bool AllowReference(Reference reference, XYZ position) { return true; }
        }
    }
}

namespace AlignTool.Commands
{
    using AlignTool.Services;

    internal static class AnnotationManagementRunner
    {
        private static IList<AnnotationManagementService.Item> GetItems(UIDocument uidoc, out IList<Element> elements)
        {
            elements = AnnotationManagementService.GetSelectionOrPick(uidoc);
            return AnnotationManagementService.Audit(uidoc.Document, uidoc.Document.ActiveView, elements).Items;
        }

        public static Result Run(ExternalCommandData data, ref string message, int mode, string title)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) { message = "No active document."; return Result.Failed; }
            try
            {
                IList<Element> elements;
                IList<AnnotationManagementService.Item> items = GetItems(uidoc, out elements);
                if (items.Count < (mode == 2 || mode == 3 ? 3 : 2))
                {
                    TaskDialog.Show(title, "Select at least " + (mode == 2 || mode == 3 ? "three" : "two") + " supported annotations with visible bounds.");
                    return Result.Cancelled;
                }

                IList<AnnotationManagementService.MovePlan> plan;
                if (mode == 0) plan = AnnotationManagementService.PlanAlign(items, uidoc.Document.ActiveView, true);
                else if (mode == 1) plan = AnnotationManagementService.PlanAlign(items, uidoc.Document.ActiveView, false);
                else if (mode == 2) plan = AnnotationManagementService.PlanDistribute(items, uidoc.Document.ActiveView, true);
                else if (mode == 3) plan = AnnotationManagementService.PlanDistribute(items, uidoc.Document.ActiveView, false);
                else
                {
                    var audit = AnnotationManagementService.Audit(uidoc.Document, uidoc.Document.ActiveView, elements);
                    plan = AnnotationManagementService.PlanCollisionResolution(audit, uidoc.Document.ActiveView, 2.0);
                    TaskDialogResult answer = TaskDialog.Show(title,
                        AnnotationManagementService.Describe(audit) + "\n\n" + AnnotationManagementService.Preview(plan) +
                        "\n\nApply the proposed collision resolution?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                    if (answer != TaskDialogResult.Yes) return Result.Cancelled;
                    int resolved = AnnotationManagementService.Apply(uidoc.Document, plan);
                    TaskDialog.Show(title, "Moved annotations: " + resolved + "\nRun Audit again to verify remaining overlaps.");
                    return Result.Succeeded;
                }

                TaskDialogResult confirm = TaskDialog.Show(title,
                    AnnotationManagementService.Preview(plan) + "\n\nApply these changes?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (confirm != TaskDialogResult.Yes) return Result.Cancelled;
                int count = AnnotationManagementService.Apply(uidoc.Document, plan);
                TaskDialog.Show(title, "Elements moved: " + count);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                AlignTool.Helpers.Logger.Error(title + " failed", ex);
                return Result.Failed;
            }
        }

        public static Result Audit(ExternalCommandData data, ref string message)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) { message = "No active document."; return Result.Failed; }
            try
            {
                IList<Element> elements = AnnotationManagementService.GetSelectionOrPick(uidoc);
                var audit = AnnotationManagementService.Audit(uidoc.Document, uidoc.Document.ActiveView, elements);
                TaskDialog.Show("Annotation Quality Audit", AnnotationManagementService.Describe(audit) +
                    (audit.Unsupported.Count == 0 ? "" : "\n\nUnsupported bounds:\n" + string.Join("\n", audit.Unsupported.Take(10))));
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AnnotationAlignHorizontalCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) { return AnnotationManagementRunner.Run(c, ref m, 0, "Align Annotation Centres Horizontally"); } }

    [Transaction(TransactionMode.Manual)]
    public class AnnotationAlignVerticalCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) { return AnnotationManagementRunner.Run(c, ref m, 1, "Align Annotation Centres Vertically"); } }

    [Transaction(TransactionMode.Manual)]
    public class AnnotationDistributeHorizontalCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) { return AnnotationManagementRunner.Run(c, ref m, 2, "Distribute Annotations Horizontally"); } }

    [Transaction(TransactionMode.Manual)]
    public class AnnotationDistributeVerticalCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) { return AnnotationManagementRunner.Run(c, ref m, 3, "Distribute Annotations Vertically"); } }

    [Transaction(TransactionMode.Manual)]
    public class AnnotationCollisionResolveCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) { return AnnotationManagementRunner.Run(c, ref m, 4, "Preview and Resolve Annotation Collisions"); } }

    [Transaction(TransactionMode.Manual)]
    public class AnnotationQualityAuditCommand : IExternalCommand
    { public Result Execute(ExternalCommandData c, ref string m, ElementSet e) { return AnnotationManagementRunner.Audit(c, ref m); } }
}
