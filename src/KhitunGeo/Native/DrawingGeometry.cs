using System.IO;
namespace KhitunGeo.Native;

internal readonly record struct DrawingBounds(double MinE, double MinN, double MaxE, double MaxN)
{
    public static DrawingBounds Empty => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);
    public bool IsEmpty => MinE > MaxE || MinN > MaxN;
    public bool IsFinite => !IsEmpty && double.IsFinite(MinE) && double.IsFinite(MinN) && double.IsFinite(MaxE) && double.IsFinite(MaxN);
    public DrawingBounds Include(double e, double n) => new(Math.Min(MinE, e), Math.Min(MinN, n), Math.Max(MaxE, e), Math.Max(MaxN, n));
    public DrawingBounds Union(DrawingBounds b) => b.IsEmpty ? this : Include(b.MinE, b.MinN).Include(b.MaxE, b.MaxN);
    public bool Intersects(DrawingBounds b) => !IsEmpty && !b.IsEmpty && MaxE >= b.MinE && MinE <= b.MaxE && MaxN >= b.MinN && MinN <= b.MaxN;
    public DrawingBounds Transform(Affine2D t)
    {
        if (IsEmpty) return this;
        var result = Empty;
        foreach (var e in new[] { MinE, MaxE }) foreach (var n in new[] { MinN, MaxN })
        { var p = t.Apply(e, n); result = result.Include(p.E, p.N); }
        return result;
    }
}
internal readonly record struct Affine2D(double A, double B, double C, double D, double E, double N)
{
    public static Affine2D Identity => new(1, 0, 0, 1, 0, 0);
    public (double E, double N) Apply(double e, double n) => (A * e + B * n + E, C * e + D * n + N);
    public (double E, double N) Vector(double e, double n) => (A * e + B * n, C * e + D * n);
    public Affine2D Compose(Affine2D child) => new(A * child.A + B * child.C, A * child.B + B * child.D,
        C * child.A + D * child.C, C * child.B + D * child.D,
        A * child.E + B * child.N + E, C * child.E + D * child.N + N);
    public bool IsFinite => double.IsFinite(A) && double.IsFinite(B) && double.IsFinite(C) && double.IsFinite(D) && double.IsFinite(E) && double.IsFinite(N);
}
internal static class DrawingGeometry
{
    public const double Tau = 2 * Math.PI;
    public static double Normalize(double a) { var n = a % Tau; return n < 0 ? n + Tau : n; }
    public static bool OnSweep(double angle, double start, double sweep)
        => Math.Abs(sweep) >= Tau - 1e-12 || (sweep >= 0 ? Normalize(angle - start) : Normalize(start - angle)) <= Math.Abs(sweep) + 1e-12;
    public static (double E1, double N1, double E2, double N2)? ClipLine(double e1, double n1, double e2, double n2, DrawingBounds b)
    {
        int Code(double e, double n) => (e < b.MinE ? 1 : e > b.MaxE ? 2 : 0) | (n < b.MinN ? 4 : n > b.MaxN ? 8 : 0);
        static double Intersection(double value, double a, double z, double otherA, double otherZ)
        {
            var origin = a / 2 + z / 2; var otherOrigin = otherA / 2 + otherZ / 2;
            var distance = Math.Abs(value / 2 - origin / 2);
            if (Math.Abs(value / 2 - a / 2) < distance)
            { origin = a; otherOrigin = otherA; distance = Math.Abs(value / 2 - a / 2); }
            if (Math.Abs(value / 2 - z / 2) < distance) { origin = z; otherOrigin = otherZ; }
            var denominator = z / 2 - a / 2;
            var slope = denominator != 0 ? (otherZ / 2 - otherA / 2) / denominator : (otherZ - otherA) / (z - a);
            return otherOrigin + (value - origin) * slope;
        }
        if (!b.IsFinite || !double.IsFinite(e1) || !double.IsFinite(n1) || !double.IsFinite(e2) || !double.IsFinite(n2)) return null;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var c1 = Code(e1, n1); var c2 = Code(e2, n2);
            if ((c1 | c2) == 0) return (e1, n1, e2, n2);
            if ((c1 & c2) != 0) return null;
            var code = c1 != 0 ? c1 : c2;
            double e, n;
            if ((code & 12) != 0)
            {
                n = (code & 8) != 0 ? b.MaxN : b.MinN;
                e = Intersection(n, n1, n2, e1, e2);
            }
            else
            {
                e = (code & 2) != 0 ? b.MaxE : b.MinE;
                n = Intersection(e, e1, e2, n1, n2);
            }
            if (!double.IsFinite(e) || !double.IsFinite(n)) return null;
            if (code == c1) { e1 = e; n1 = n; } else { e2 = e; n2 = n; }
        }
        return null;
    }
    public static DrawingPrimitive FromBulge(string layer, double e1, double n1, double e2, double n2, double bulge)
    {
        if (!double.IsFinite(bulge)) throw new InvalidDataException("Некорректная дуга полилинии.");
        var de = e2 - e1; var dn = n2 - n1;
        if (de == 0 && dn == 0) return new(DrawingKind.Point, layer, e1, n1);
        if (bulge == 0) return new(DrawingKind.Line, layer, e1, n1, e2, n2);
        var factor = (1 / bulge - bulge) / 4;
        var ce = e1 / 2 + e2 / 2 - dn * factor; var cn = n1 / 2 + n2 / 2 + de * factor;
        var radius = Math.Sqrt(de * de + dn * dn) * (Math.Abs(bulge) / 4 + 1 / (4 * Math.Abs(bulge)));
        var re = e1 - ce; var rn = n1 - cn;
        // Keep the exact starting vertex and local delta angle. Reconstructing a
        // shallow arc from its distant center would lose metres at CAD coordinates.
        var p = new DrawingPrimitive(DrawingKind.Curve, layer, ce, cn, Radius: radius,
            Sweep: 4 * Math.Atan(bulge), UE: re, UN: rn, VE: -rn, VN: re, AnchorE: e1, AnchorN: n1);
        if (!p.IsValid) throw new InvalidDataException("Некорректная геометрия полилинии.");
        return p;
    }
}
