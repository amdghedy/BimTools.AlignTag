using System;
using System.Linq;
using Autodesk.Revit.UI;

namespace AlignTool.Helpers
{
    public static class RibbonHelper
    {
        public static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tabName, string panelName)
        {
            try { app.CreateRibbonTab(tabName); } catch { /* tab may exist */ }
            var panels = app.GetRibbonPanels(tabName);
            var existing = panels.FirstOrDefault(p => p.Name == panelName);
            if (existing != null) return existing;
            return app.CreateRibbonPanel(tabName, panelName);
        }

        public static PushButtonData CreatePushButtonData(string name, string text, string className, string toolTip, string longDesc = null, string iconName = null)
        {
            string asm = typeof(App).Assembly.Location;
            var data = new PushButtonData(name, text, asm, className)
            {
                ToolTip = toolTip,
                LongDescription = longDesc ?? toolTip
            };
            if (!string.IsNullOrEmpty(iconName))
            {
                var large = IconLoader.GetIcon(iconName, large: true);
                var small = IconLoader.GetIcon(iconName, large: false);
                if (large != null) data.LargeImage = large;
                if (small != null) data.Image = small;
            }
            return data;
        }

        public static void AddStackedTriple(RibbonPanel panel, PushButtonData a, PushButtonData b, PushButtonData c)
        {
            panel.AddStackedItems(a, b, c);
        }

        public static void AddPushButton(RibbonPanel panel, string name, string text, string className, string toolTip, string longDesc = null, string iconName = null)
        {
            panel.AddItem(CreatePushButtonData(name, text, className, toolTip, longDesc, iconName));
        }
    }
}
