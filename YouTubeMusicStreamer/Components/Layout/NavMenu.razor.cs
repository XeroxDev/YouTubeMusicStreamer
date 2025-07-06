namespace YouTubeMusicStreamer.Components.Layout;

public partial class NavMenu
{
    private bool _isDebug = false;

    public NavMenu()
    {
#if DEBUG
        IsDebugInitializer();
#else
        IsNotDebugInitializer();
#endif
    }

    private void IsDebugInitializer()
    {
        _isDebug = true;
    }

    private void IsNotDebugInitializer()
    {
    }
}