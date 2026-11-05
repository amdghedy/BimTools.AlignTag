using System;
using System.Collections.Generic;
using System.Linq;
using AlignTool.Helpers;
using AlignTool.Models;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    public class LeaderCrossingService : ILeaderService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly AppSettings _settings;

        public LeaderCrossingService(Document doc, View view, AppSettings settings = null)
        {
            _doc = doc;
            _view = view;
            _settings = settings ?? SettingsService.Instance.Current;
        }

        public AlignmentResult ResolveCrossings(IList<IndependentTag> tags)
        {
            var result = new AlignmentResult { TagsProcessed = tags?.Count ?? 0 };
            if (tags == null || tags.Count < 2)
            {
                result.Success = false;
                result.Message = "Need at least two tags.";
                return result;
            }

            var movable = tags.Where(t => !(_settings.SkipPinnedTags && t.Pinned)).ToList();
            if (movable.Count == 0)
            {
                result.Success = false;
                result.Message = "All tags are pinned.";
                return result;
            }

            // Stack by host Y order around median X (constrained moves)
            var ordered = movable
                .Select(t => new { Tag = t, End = TagInfo.GetLeaderEndSafe(t, _doc, _view, out _) })
                .OrderBy(x => x.End != null ? x.End.Y : x.Tag.TagHeadPosition.Y)
                .ToList();

            double spacing = UnitHelper.MmToFeet(_settings.StackSpacingMm);
            double medianX = ordered.Average(x => x.Tag.TagHeadPosition.X);
            double midY = ordered.Average(x => x.Tag.TagHeadPosition.Y);
            double startY = midY - (ordered.Count - 1) * spacing * 0.5;
            double maxMove = UnitHelper.MmToFeet(_settings.MaxMoveMm);

            using (var tg = new TransactionGroup(_doc, "Stack Tags"))
            {
                tg.Start();
                for (int i = 0; i < ordered.Count; i++)
                {
                    var tag = ordered[i].Tag;
                    XYZ h = tag.TagHeadPosition;
                    XYZ dest = new XYZ(medianX, startY + i * spacing, h.Z);
                    if (h.DistanceTo(dest) > maxMove)
                    {
                        XYZ dir = (dest - h).Normalize();
                        dest = h + dir * maxMove;
                    }
                    using (var t = new Transaction(_doc, "Move Tag"))
                    {
                        t.Start();
                        if (tag.TrySetHeadPosition(dest))
                        {
                            t.Commit();
                            result.TagsMoved++;
                            result.MovedIds.Add(tag.Id);
                        }
                        else
                        {
                            t.RollBack();
                            result.TagsFailed++;
                        }
                    }
                }
                tg.Assimilate();
            }

            result.Success = true;
            result.MovedCount = result.TagsMoved;
            result.Message = $"{result.TagsMoved} tag(s) stacked to reduce crossings.";
            return result;
        }
    }
}
