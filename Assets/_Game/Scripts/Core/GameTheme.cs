using UnityEngine;

namespace Ashfold
{
    /// <summary>Палитра в духе VG: тёмный фон, золото, бирюза. Не копирует ассеты SEMC.</summary>
    public static class GameTheme
    {
        public static readonly Color Bg = Hex(0x0B1218);
        public static readonly Color BgPanel = Hex(0x121C26);
        public static readonly Color BgPanelSoft = Hex(0x182430);
        public static readonly Color Gold = Hex(0xD4B45A);
        public static readonly Color GoldDim = Hex(0x8A7340);
        public static readonly Color Teal = Hex(0x3DCEC7);
        public static readonly Color Crimson = Hex(0xC44545);
        public static readonly Color Text = Hex(0xEDE6D6);
        public static readonly Color TextMuted = Hex(0x8B97A3);
        public static readonly Color Line = new Color(0.83f, 0.71f, 0.35f, 0.35f);

        public static Color Hex(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        }
    }
}
