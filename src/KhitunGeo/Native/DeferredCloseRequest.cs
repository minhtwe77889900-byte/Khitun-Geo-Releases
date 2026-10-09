namespace KhitunGeo.Native;

// Owned by the UI thread; worker completion replays the ordinary close/save flow once.
internal sealed class DeferredCloseRequest
{
    private bool requested;
    public void Request() => requested = true;
    public void Complete(Action close)
    {
        if (!requested) return;
        requested = false;
        close();
    }
}
