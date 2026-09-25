using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Colour palette of the editor UI, modelled on the Unity dark skin.
    /// Used by the UI builder (Placement System ▸ Rebuild UI) and by runtime
    /// UI scripts, so a colour only has to be changed here.
    /// </summary>
    public static class UITheme
    {
        public static readonly Color PanelBackground = Hex(0x383838, 0.98f);
        public static readonly Color WindowTab       = Hex(0x282828);
        public static readonly Color SectionHeader   = Hex(0x3E3E3E);
        public static readonly Color Divider         = Hex(0x232323);
        public static readonly Color FieldBackground = Hex(0x2A2A2A);
        public static readonly Color Hover           = Hex(0x484848);

        public static readonly Color Button          = Hex(0x585858);
        public static readonly Color ButtonHover     = Hex(0x676767);
        public static readonly Color ButtonPressed   = Hex(0x464646);

        public static readonly Color Accent          = Hex(0x2C5D87);
        public static readonly Color AccentBright    = Hex(0x4C8FD6);

        public static readonly Color Danger          = Hex(0x8A3030);
        public static readonly Color DangerHover     = Hex(0xA83A3A);

        public static readonly Color Text            = Hex(0xD2D2D2);
        public static readonly Color TextDim         = Hex(0x8F8F8F);

        public static readonly Color AxisX           = Hex(0xE0645A);
        public static readonly Color AxisY           = Hex(0x86C455);
        public static readonly Color AxisZ           = Hex(0x5A9AE6);

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
        }
    }
}
