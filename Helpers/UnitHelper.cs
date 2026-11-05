using Autodesk.Revit.DB;

namespace AlignTool.Helpers
{
    /// <summary>
    /// Unit helpers for Revit 2020 (DisplayUnitType API).
    /// </summary>
    public static class UnitHelper
    {
        public static double MmToFeet(double mm)
        {
            return UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS);
        }

        public static double FeetToMm(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, DisplayUnitType.DUT_MILLIMETERS);
        }

        public static string FormatLength(Document doc, double feet)
        {
            try
            {
                return string.Format("{0:F1} mm", FeetToMm(feet));
            }
            catch
            {
                return feet.ToString("F3") + " ft";
            }
        }
    }
}
