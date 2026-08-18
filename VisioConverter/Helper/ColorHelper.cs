using System.Text.RegularExpressions;

namespace VisioConverter.Helper
{
    public class ColorHelper
    {
        public static readonly string[] BUILTIN_COLORS = 
        [
          "#000000", "#FFFFFF", "#FF0000", "#00FF00", "#0000FF", "#FFFF00",
          "#FF00FF", "#00FFFF", "#800000", "#008000", "#000080", "#808000",
          "#800080", "#008080", "#C0C0C0", "#E6E6E6", "#CDCDCD", "#B3B3B3",
          "#9A9A9A", "#808080", "#666666", "#4D4D4D", "#333333", "#1A1A1A"
        ];

        public static Dictionary<int, string> ColorPalette = BUILTIN_COLORS
                                                            .Select((color, index) => new { Index = index, Color = color })
                                                            .ToDictionary(item => item.Index, item => item.Color);

        public static string GetColor(string value, Dictionary<int, string> colorPalette = null)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            var s = value.Trim();

            if (string.IsNullOrEmpty(s) || s == "Themed")
            {
                return null;
            }

            if (s.StartsWith("#"))
            {
                return s;
            }

            if (s.Contains(","))
            {
                var parts = s.Split(",").Select(item => double.Parse(item)).ToList();

                if (parts.Count >= 3)
                {
                    return "#" + string.Join("", parts.Take(3).Select(v => Convert.ToString((int)Math.Max(0, Math.Min(255, v)), 16).PadLeft(2, '0')));
                }
            }

            if (colorPalette != null && int.TryParse(s, out var idx))
            {
                if (colorPalette.ContainsKey(idx))
                {
                    return colorPalette[idx];
                }

                if (idx >= 0 && idx < BUILTIN_COLORS.Length)
                {
                    return BUILTIN_COLORS[idx];
                }
            }

            return null;
        }

        public static bool IsLightColor(string color)
        {
            if (string.IsNullOrEmpty(color) || !Regex.IsMatch(color, @"^#[0-9A-F]{6}$", RegexOptions.IgnoreCase))
            {
                return false;
            }

            var r = Convert.ToInt32(color.Substring(1, 2), 16);
            var g = Convert.ToInt32(color.Substring(3, 2), 16);
            var b = Convert.ToInt32(color.Substring(5, 2), 16);

            var luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;

            return luminance >= 0.7;
        }
    }
}
