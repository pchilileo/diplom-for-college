using System;

namespace PlacementSystem
{
    /// <summary>
    /// Short user-facing messages ("these points are already connected", ...).
    /// Any system can post one; the status bar shows it for a few seconds.
    /// </summary>
    public static class EditorNotifications
    {
        public static event Action<string> MessagePosted;

        public static void Post(string message)
        {
            MessagePosted?.Invoke(message);
        }
    }
}
