using System;

namespace UnderwaterGliderTwin.UI
{
    public enum RuntimeUiValidationProfile
    {
        BootstrapOnly,
        EnabledPanels,
        Strict
    }

    [Flags]
    public enum RuntimeUiPanelFlags
    {
        None = 0,
        Dashboard = 1 << 0,
        Status = 1 << 1,
        DataInput = 1 << 2,
        Playback = 1 << 3,
        OceanToolbar = 1 << 4,
        All = Dashboard | Status | DataInput | Playback | OceanToolbar
    }
}
