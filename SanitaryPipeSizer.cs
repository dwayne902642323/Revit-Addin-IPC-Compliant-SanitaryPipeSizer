using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanitaryPipeSizing
{
    public static class SanitaryPipeSizer
    {
        public static int SizeSelectedSanitaryPipes(UIDocument uidoc, Document doc)
        {
            var pipes = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<Pipe>()
                .Where(p => p.PipeSystemType == PipeSystemType.Sanitary)
                .OrderByDescending(p => GetPipeZElevation(p))
                .ToList();

            double maxUpstreamDiameter = 0.0;
            int countSized = 0;

            using (Transaction tx = new Transaction(doc, "Size Sanitary Pipes"))
            {
                tx.Start();

                foreach (var pipe in pipes)
                {
                    double dfu = pipe.get_Parameter(BuiltInParameter.RBS_DRAIN_FIXTURE_UNITS_PARAM)?.AsDouble() ?? 0;
                    if (dfu < 0.01) continue;

                    double slope = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_SLOPE_PARAM)?.AsDouble() ?? 0.0104;
                    bool isVertical = IsPipeVerticalStack(pipe);

                    double baseDiameter = isVertical
                        ? GetVerticalStackDiameter(dfu)
                        : GetHorizontalDrainDiameter(dfu, slope);

                    if (!isVertical)
                        baseDiameter = ApplyBranchLimits(dfu, baseDiameter);

                    double finalDiameterFt = Math.Max(baseDiameter / 12.0, maxUpstreamDiameter);

                    var diaParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    if (diaParam != null && diaParam.StorageType == StorageType.Double)
                    {
                        diaParam.Set(finalDiameterFt);
                        countSized++;
                    }

                    maxUpstreamDiameter = Math.Max(maxUpstreamDiameter, finalDiameterFt);
                }

                tx.Commit();
            }

            return countSized;
        }

        private static double GetPipeZElevation(Pipe pipe)
        {
            var curve = (pipe.Location as LocationCurve)?.Curve;
            return curve != null ? Math.Max(curve.GetEndPoint(0).Z, curve.GetEndPoint(1).Z) : 0.0;
        }

        private static bool IsPipeVerticalStack(Pipe pipe)
        {
            ConnectorManager cm = pipe.ConnectorManager;
            foreach (Connector conn in cm.Connectors)
            {
                foreach (Connector refConn in conn.AllRefs)
                {
                    if (refConn.Owner is Pipe other && other.Id != pipe.Id)
                    {
                        XYZ p0 = other.get_EndPoint(0);
                        XYZ p1 = other.get_EndPoint(1);
                        double dz = Math.Abs(p0.Z - p1.Z);
                        double dx = Math.Abs(p0.X - p1.X);
                        double dy = Math.Abs(p0.Y - p1.Y);
                        if (dz > dx && dz > dy) return true;
                    }
                }
            }
            return false;
        }

        private static double GetVerticalStackDiameter(double dfu)
        {
            var table = new SortedDictionary<double, double>
            {
                { 2, 1.5 }, { 4, 2 }, { 6, 2.5 }, { 12, 3 }, { 42, 4 },
                { 72, 5 }, { 120, 6 }, { 250, 8 }, { 500, 10 }, { 840, 12 }
            };
            return table.FirstOrDefault(kvp => dfu <= kvp.Key).Value != 0 ? table.First(kvp => dfu <= kvp.Key).Value : 15;
        }

        private static double GetHorizontalDrainDiameter(double dfu, double slope)
        {
            if (slope < 0.0104) return 4;

            var table = new SortedDictionary<double, double>
            {
                { 3, 1.5 }, { 6, 2 }, { 9, 2.5 }, { 12, 3 }, { 26, 4 },
                { 50, 5 }, { 75, 6 }, { 150, 8 }, { 216, 10 }, { 300, 12 }, { 575, 15 }
            };
            return table.FirstOrDefault(kvp => dfu <= kvp.Key).Value != 0 ? table.First(kvp => dfu <= kvp.Key).Value : 4;
        }

        private static double ApplyBranchLimits(double dfu, double currentSize)
        {
            var limits = new SortedDictionary<double, double>
            {
                { 1.5, 3 }, { 2.0, 6 }, { 2.5, 9 }, { 3.0, 20 }, { 4.0, 160 },
                { 5.0, 360 }, { 6.0, 620 }, { 8.0, 1400 }, { 10.0, 2500 }
            };

            foreach (var kvp in limits)
            {
                if (dfu <= kvp.Value && kvp.Key >= currentSize)
                    return kvp.Key;
            }

            return currentSize;
        }

        public static XYZ get_EndPoint(this Pipe pipe, int index)
        {
            return (pipe.Location as LocationCurve)?.Curve.GetEndPoint(index) ?? XYZ.Zero;
        }
    }
}