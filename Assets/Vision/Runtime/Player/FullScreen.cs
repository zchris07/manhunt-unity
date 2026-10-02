using System;
using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// Starts the player borderless full screen at the display's native resolution, overriding a windowed
    /// mode Unity remembered from an earlier run. Command-line screen options (and capture runs) win.
    /// Alt+Enter still switches to a window.
    /// </summary>
    public static class FullScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            if (Application.isEditor || !ShouldApply(Environment.GetCommandLineArgs())) return;
            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
        }

        public static bool ShouldApply(string[] args)
        {
            foreach (string a in args)
                if (a.StartsWith("-screen-", StringComparison.OrdinalIgnoreCase) || a == "-visionCapture" || a == "-popupwindow") return false;
            return true;
        }
    }
}
