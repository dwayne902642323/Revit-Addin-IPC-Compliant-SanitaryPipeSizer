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
        /// <summary>
        /// Sizes all selected sanitary pipes in the active Revit view based on IPC DFU tables.
        /// </summary>
        public static int SizeSelectedSanitaryPipes(UIDocument uidoc, Document doc)
        {
            // Get all selected sanitary pipes, ordered from top to bottom (descending Z)
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
                    // Retrieve DFU value from Revit parameter
                    double dfu = pipe.get_Parameter(BuiltInParameter.RBS_DRAIN_FIXTURE_UNITS_PARAM)?.AsDouble() ?? 0;
                    if (dfu < 0.01) continue; // Skip dry or uninitialized pipes

                    // Get pipe slope (used for horizontal sizing)
                    double slope = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_SLOPE_PARAM)?.AsDouble() ?? 0.0104;

                    // Determine if pipe is part of a vertical stack or horizontal drain
                    bool isVertical = IsPipeVerticalStack(pipe);

                    // 🔹 Step 5: Apply Vertical Pipe Sizing – IPC Table 710.1(1)
                    // 🔹 Step 6: Apply Horizontal Drain Sizing – IPC Table 710.1(2) + §704.1
                    double baseDiameter = isVertical
                        ? GetVerticalStackDiameter(dfu)
                        : GetHorizontalDrainDiameter(dfu, slope);

                    // 🔹 Step 7: Apply Horizontal Branch Limits – IPC Table 703.2
                    if (!isVertical)
                        baseDiameter = ApplyBranchLimits(dfu, baseDiameter);

                    // 🔹 Step 8: Enforce No Reductions in Flow Direction – IPC §710.1.8
                    double finalDiameterFt = Math.Max(baseDiameter / 12.0, maxUpstreamDiameter);

                    // Set new diameter on pipe (in feet, since Revit internal units are imperial)
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

        /// <summary>
        /// Gets the top Z elevation of the pipe in the model.
        /// Used to size from upstream to downstream (top-down).
        /// </summary>
        private static double GetPipeZElevation(Pipe pipe)
        {
            var curve = (pipe.Location as LocationCurve)?.Curve;
            return curve != null ? Math.Max(curve.GetEndPoint(0).Z, curve.GetEndPoint(1).Z) : 0.0;
        }

        /// <summary>
        /// Detects whether the pipe is a vertical stack based on geometric orientation.
        /// Vertical stack is defined as having more Z-axis than X/Y delta between endpoints.
        /// </summary>
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

        /// <summary>
        /// IPC Table 710.1(1) — Maximum DFU for Vertical Stacks
        /// Maps DFU to required pipe diameter (inches).
        /// </summary>
        private static double GetVerticalStackDiameter(double dfu)
        {
            var table = new SortedDictionary<double, double>
            {
                { 2, 1.5 }, { 4, 2 }, { 6, 2.5 }, { 12, 3 }, { 42, 4 },
                { 72, 5 }, { 120, 6 }, { 250, 8 }, { 500, 10 }, { 840, 12 }
            };

            // Return the first diameter where DFU fits under max limit
            return table.FirstOrDefault(kvp => dfu <= kvp.Key).Value != 0
                ? table.First(kvp => dfu <= kvp.Key).Value
                : 15; // Default to 15" if above chart range
        }

        /// <summary>
        /// IPC Table 710.1(2) — Maximum DFU for Horizontal Drains
        /// IPC §704.1 — Minimum Slope by Pipe Diameter
        /// </summary>
        private static double GetHorizontalDrainDiameter(double dfu, double slope)
        {
            // IPC §704.1 states that slope must meet minimum thresholds.
            // If below 1/8" per foot (0.0104 ft/ft), we conservatively default to 4".
            if (slope < 0.0104) return 4;

            var table = new SortedDictionary<double, double>
            {
                { 3, 1.5 }, { 6, 2 }, { 9, 2.5 }, { 12, 3 }, { 26, 4 },
                { 50, 5 }, { 75, 6 }, { 150, 8 }, { 216, 10 }, { 300, 12 }, { 575, 15 }
            };

            return table.FirstOrDefault(kvp => dfu <= kvp.Key).Value != 0
                ? table.First(kvp => dfu <= kvp.Key).Value
                : 4; // Default to 4" if out of range
        }

        /// <summary>
        /// IPC Table 703.2 — Maximum DFU on Horizontal Branches
        /// Applies upper limit by DFU and pipe diameter to enforce code-legal combinations.
        /// </summary>
        private static double ApplyBranchLimits(double dfu, double currentSize)
        {
            var limits = new SortedDictionary<double, double>
            {
                { 1.5, 3 }, { 2.0, 6 }, { 2.5, 9 }, { 3.0, 20 }, { 4.0, 160 },
                { 5.0, 360 }, { 6.0, 620 }, { 8.0, 1400 }, { 10.0, 2500 }
            };

            // Find smallest legal pipe size ≥ current size that can support the DFU
            foreach (var kvp in limits)
            {
                if (dfu <= kvp.Value && kvp.Key >= currentSize)
                    return kvp.Key;
            }

            return currentSize; // No change if no limit violated
        }

        /// <summary>
        /// Extension method to get a pipe endpoint, with null safety.
        /// </summary>
        public static XYZ get_EndPoint(this Pipe pipe, int index)
        {
            return (pipe.Location as LocationCurve)?.Curve.GetEndPoint(index) ?? XYZ.Zero;
        }
    }
}
