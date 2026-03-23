using AROKIS.Backend.Models;

namespace AROKIS.Backend.Services;

public enum ThicknessMode
{
    OuterMost,
    MaxInsideSegment
}

public static class ThicknessCalculator
{
    public static (double cx, double cy) CenterOf(IReadOnlyList<CablePoint> pts)
    {
        double sx = 0, sy = 0;
        foreach (var p in pts) { sx += p.X; sy += p.Y; }
        return (sx / pts.Count, sy / pts.Count);
    }

    public static double? MeasureUnitsByAngle(
        IReadOnlyList<CablePoint> contour,
        double angleDeg,
        ThicknessMode mode = ThicknessMode.OuterMost)
    {
        if (contour == null || contour.Count < 2) return null;

        var (cx, cy) = CenterOf(contour);

        double rad = angleDeg * Math.PI / 180.0;
        double dx = Math.Cos(rad);
        double dy = Math.Sin(rad);

        double nx = -dy, ny = dx;

        const double eps = 1e-9;
        var ts = new List<double>(8);

        for (int i = 0; i < contour.Count; i++)
        {
            var a = contour[i];
            var b = contour[(i + 1) % contour.Count];

            double da = (a.X - cx) * nx + (a.Y - cy) * ny;
            double db = (b.X - cx) * nx + (b.Y - cy) * ny;

            if (Math.Abs(da - db) < eps) continue;

            bool crosses =
                (da > 0 && db < 0) || (da < 0 && db > 0) ||
                Math.Abs(da) < eps || Math.Abs(db) < eps;

            if (!crosses) continue;

            double u = da / (da - db);
            if (u < -1e-6 || u > 1 + 1e-6) continue;

            double ix = a.X + (b.X - a.X) * u;
            double iy = a.Y + (b.Y - a.Y) * u;

            double t = (ix - cx) * dx + (iy - cy) * dy;
            ts.Add(t);
        }

        if (ts.Count < 2) return null;
        ts.Sort();

        var uniq = new List<double>(ts.Count);
        foreach (var t in ts)
            if (uniq.Count == 0 || Math.Abs(t - uniq[^1]) > 1e-6)
                uniq.Add(t);

        if (uniq.Count < 2) return null;

        if (mode == ThicknessMode.OuterMost)
            return uniq[^1] - uniq[0];

        double mx = 0;
        for (int i = 0; i + 1 < uniq.Count; i += 2)
            mx = Math.Max(mx, uniq[i + 1] - uniq[i]);

        return mx;
    }
}
