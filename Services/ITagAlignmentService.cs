using System.Collections.Generic;
using AlignTool.Models;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    public interface ITagAlignmentService
    {
        AlignmentResult AlignHorizontally(IList<IndependentTag> tags);
        AlignmentResult AlignVertically(IList<IndependentTag> tags);
    }
}
