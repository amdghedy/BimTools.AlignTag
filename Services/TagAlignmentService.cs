using System.Collections.Generic;
using System.Linq;
using AlignTool.Helpers;
using AlignTool.Models;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    public class TagAlignmentService : ITagAlignmentService
    {
        private readonly Document _doc;
        private readonly View _view;

        public TagAlignmentService(Document doc, View view)
        {
            _doc = doc;
            _view = view;
        }

        public AlignmentResult AlignHorizontally(IList<IndependentTag> tags)
        {
            if (tags == null || tags.Count < 2)
                return AlignmentResult.Fail("Select at least two tags.");
            double y = tags[0].TagHeadPosition.Y;
            int moved = 0;
            using (var tg = new TransactionGroup(_doc, "Align H"))
            {
                tg.Start();
                for (int i = 1; i < tags.Count; i++)
                {
                    if (tags[i].Pinned) continue;
                    XYZ p = tags[i].TagHeadPosition;
                    using (var t = new Transaction(_doc, "Align"))
                    {
                        t.Start();
                        if (tags[i].TrySetHeadPosition(new XYZ(p.X, y, p.Z))) { t.Commit(); moved++; }
                        else t.RollBack();
                    }
                }
                tg.Assimilate();
            }
            return AlignmentResult.Ok($"{moved} tag(s) aligned horizontally.", moved);
        }

        public AlignmentResult AlignVertically(IList<IndependentTag> tags)
        {
            if (tags == null || tags.Count < 2)
                return AlignmentResult.Fail("Select at least two tags.");
            double x = tags[0].TagHeadPosition.X;
            int moved = 0;
            using (var tg = new TransactionGroup(_doc, "Align V"))
            {
                tg.Start();
                for (int i = 1; i < tags.Count; i++)
                {
                    if (tags[i].Pinned) continue;
                    XYZ p = tags[i].TagHeadPosition;
                    using (var t = new Transaction(_doc, "Align"))
                    {
                        t.Start();
                        if (tags[i].TrySetHeadPosition(new XYZ(x, p.Y, p.Z))) { t.Commit(); moved++; }
                        else t.RollBack();
                    }
                }
                tg.Assimilate();
            }
            return AlignmentResult.Ok($"{moved} tag(s) aligned vertically.", moved);
        }
    }
}
