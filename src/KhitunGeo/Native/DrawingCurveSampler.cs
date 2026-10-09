namespace KhitunGeo.Native;

internal sealed record DrawingCurveSamples(IReadOnlyList<(double E, double N)> Points, bool DetailLimited);
internal static class DrawingCurveSampler
{
    public static DrawingCurveSamples Sample(DrawingPrimitive curve, double screenScale)
    {
        if (!curve.IsCurve || !curve.IsValid || !double.IsFinite(screenScale) || screenScale <= 0)
            throw new ArgumentException("Некорректная кривая или масштаб.");
        var u = curve.U; var v = curve.V;
        // Linear interpolation error <= max|second derivative| * angleStep² / 8.
        // The sum of axis norms is a conservative bound for any affine ellipse.
        var magnitude = Math.Sqrt(u.E * u.E + u.N * u.N) + Math.Sqrt(v.E * v.E + v.N * v.N);
        var required = Math.Ceiling(Math.Abs(curve.SweepRadians) * Math.Sqrt(magnitude * screenScale / 6));
        var limited = !double.IsFinite(required) || required > 1024;
        var segments = limited ? 1024 : Math.Max(1, (int)required);
        var points = new (double E, double N)[segments + 1];
        var start = DrawingGeometry.Normalize(curve.StartAngle);
        for (var i = 0; i <= segments; i++) points[i] = curve.At(start + curve.SweepRadians * i / segments);
        return new(Array.AsReadOnly(points), limited);
    }
}
