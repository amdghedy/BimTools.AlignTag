using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    public static class LeaderGeometryService
    {
        public static bool SegmentsCross(XYZ a1, XYZ a2, XYZ b1, XYZ b2)
        {
            if (a1 == null || a2 == null || b1 == null || b2 == null) return false;
            double d1 = Cross2d(a2 - a1, b1 - a1);
            double d2 = Cross2d(a2 - a1, b2 - a1);
            double d3 = Cross2d(b2 - b1, a1 - b1);
            double d4 = Cross2d(b2 - b1, a2 - b1);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
                && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Cross2d(XYZ u, XYZ v) => u.X * v.Y - u.Y * v.X;
    }
}
