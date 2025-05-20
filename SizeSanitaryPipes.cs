using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace SanitaryPipeSizing
{
    [Transaction(TransactionMode.Manual)]
    public class SizeSanitaryPipes : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            int pipesSized = SanitaryPipeSizer.SizeSelectedSanitaryPipes(uidoc, doc);

            TaskDialog.Show("Sanitary Pipe Sizing", $"Finished sizing {pipesSized} sanitary pipe(s).");

            return Result.Succeeded;
        }
    }
}