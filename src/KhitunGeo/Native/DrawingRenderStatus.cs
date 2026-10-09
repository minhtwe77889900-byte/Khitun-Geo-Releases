namespace KhitunGeo.Native;

internal sealed class DrawingRenderStatus
{
    public bool Truncated { get; private set; }
    public bool DetailLimited { get; private set; }
    public bool Record(bool fullPaint, bool truncated, bool detailLimited)
    {
        var nextTruncated = truncated || (!fullPaint && Truncated);
        var nextDetail = detailLimited || (!fullPaint && DetailLimited);
        var changed = nextTruncated != Truncated || nextDetail != DetailLimited;
        Truncated = nextTruncated; DetailLimited = nextDetail; return changed;
    }
}
