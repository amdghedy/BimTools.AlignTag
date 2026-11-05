using System;
using System.Collections.Generic;
using System.Linq;
using AlignTool.Helpers;
using AlignTool.Models;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    public class AlignBoxService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly AppSettings _settings;

        public AlignBoxService(Document doc, View view, AppSettings settings = null)
        {
            _doc = doc;
            _view = view;
            _settings = settings ?? SettingsService.Instance.Current;
        }

        public enum AlignMode { Left, Right, Top, Bottom, Center, Middle }

        public AlignmentResult Align(IList<IndependentTag> tags, AlignMode mode)
        {
            var result = new AlignmentResult { TagsProcessed = tags?.Count ?? 0 };
            if (tags == null || tags.Count < 2)
            {
                result.Success = false;
                result.Message = "Select at least two tags.";
                return result;
            }

            var movable = tags.Where(t => !(_settings.SkipPinnedTags && t.Pinned)).ToList();
            if (movable.Count < 2)
            {
                result.Success = false;
                result.Message = "Not enough movable tags.";
                return result;
            }

            var heads = movable.Select(t => t.TagHeadPosition).ToList();
            double target;
            switch (mode)
            {
                case AlignMode.Left: target = heads.Min(p => p.X); break;
                case AlignMode.Right: target = heads.Max(p => p.X); break;
                case AlignMode.Center: target = (heads.Min(p => p.X) + heads.Max(p => p.X)) * 0.5; break;
                case AlignMode.Top: target = heads.Max(p => p.Y); break;
                case AlignMode.Bottom: target = heads.Min(p => p.Y); break;
                case AlignMode.Middle: target = (heads.Min(p => p.Y) + heads.Max(p => p.Y)) * 0.5; break;
                default:
                    result.Success = false;
                    result.Message = "Unknown align mode.";
                    return result;
            }

            bool horizontal = mode == AlignMode.Left || mode == AlignMode.Right || mode == AlignMode.Center;

            using (var tg = new TransactionGroup(_doc, "Align Tags"))
            {
                tg.Start();
                foreach (var tag in movable)
                {
                    XYZ h = tag.TagHeadPosition;
                    XYZ dest = horizontal
                        ? new XYZ(target, h.Y, h.Z)
                        : new XYZ(h.X, target, h.Z);
                    using (var t = new Transaction(_doc, "Align Tag"))
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

            result.Success = result.TagsFailed == 0;
            result.MovedCount = result.TagsMoved;
            result.Message = $"{result.TagsMoved} tag(s) aligned.";
            return result;
        }

        public AlignmentResult Distribute(IList<IndependentTag> tags, bool horizontal)
        {
            var result = new AlignmentResult { TagsProcessed = tags?.Count ?? 0 };
            if (tags == null || tags.Count < 3)
            {
                result.Success = false;
                result.Message = "Select at least three tags to distribute.";
                return result;
            }

            var movable = tags.Where(t => !(_settings.SkipPinnedTags && t.Pinned)).ToList();
            if (movable.Count < 3)
            {
                result.Success = false;
                result.Message = "Not enough movable tags.";
                return result;
            }

            var ordered = horizontal
                ? movable.OrderBy(t => t.TagHeadPosition.X).ToList()
                : movable.OrderBy(t => t.TagHeadPosition.Y).ToList();

            XYZ first = ordered.First().TagHeadPosition;
            XYZ last = ordered.Last().TagHeadPosition;
            double span = horizontal ? (last.X - first.X) : (last.Y - first.Y);
            if (Math.Abs(span) < 1e-6)
            {
                result.Success = false;
                result.Message = "Tags are coincident; cannot distribute.";
                return result;
            }

            double step = span / (ordered.Count - 1);

            using (var tg = new TransactionGroup(_doc, horizontal ? "Distribute Horizontal" : "Distribute Vertical"))
            {
                tg.Start();
                for (int i = 1; i < ordered.Count - 1; i++)
                {
                    var tag = ordered[i];
                    XYZ h = tag.TagHeadPosition;
                    XYZ dest = horizontal
                        ? new XYZ(first.X + i * step, h.Y, h.Z)
                        : new XYZ(h.X, first.Y + i * step, h.Z);

                    using (var t = new Transaction(_doc, "Distribute Tag"))
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

            result.Success = result.TagsFailed == 0;
            result.MovedCount = result.TagsMoved;
            result.Message = $"{result.TagsMoved} tag(s) distributed.";
            return result;
        }
    }
}
