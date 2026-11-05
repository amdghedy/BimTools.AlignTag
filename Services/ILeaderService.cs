using System.Collections.Generic;
using AlignTool.Models;
using Autodesk.Revit.DB;

namespace AlignTool.Services
{
    public interface ILeaderService
    {
        AlignmentResult ResolveCrossings(IList<IndependentTag> tags);
    }
}
