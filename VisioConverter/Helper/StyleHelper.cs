using System.Text.RegularExpressions;
using System.Xml.Linq;
using VisioConverter.Extension;
using VisioConverter.Model;

namespace VisioConverter.Helper
{
    public class StyleHelper
    {
        public static readonly Dictionary<int, string> QUICKSTYLE_COLOR_MAP = new Dictionary<int, string>()
        {
          {0, "dk1"},
          {1, "lt1"},
          {2, "dk2"},
          {3, "lt2"},
          {4, "accent1"},
          {5, "accent2"},
          {6, "accent3"},
          {7, "accent4"},
          {8, "accent5"},
          {9, "accent6"},
          {100, "dk1"},
          {101, "lt1"},
          {102, "dk2"},
          {103, "accent1"},
          {104, "accent2"},
          {105, "accent3"},
          {106, "accent4"},
          {107, "accent5"},
          {108, "accent6" }
        };

        public static Dictionary<string, string> GetThemeColors(XDocument themeDoc)
        {
            var themeColors = new Dictionary<string, string>();
            var clrScheme = themeDoc.Root.Child("themeElements")?.Child("clrScheme");

            if (clrScheme == null)
                return themeColors;

            string[] names = ["dk1", "lt1", "dk2", "lt2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink"];

            foreach (var name in names)
            {
                var el = clrScheme.Child(name);

                if (el == null)
                    continue;

                var srgb = el.Child("srgbClr");
                var sysClr = el.Child("sysClr");

                if (srgb != null)
                {
                    var val = srgb.GetAttributeValue("val");

                    if (!string.IsNullOrEmpty(val))
                        themeColors[name] = $"#{val}";
                }
                else if (sysClr != null)
                {
                    var val = sysClr.GetAttributeValue("lastClr") ?? sysClr.GetAttributeValue("val");

                    if (!string.IsNullOrEmpty(val) && val.Length == 6)
                        themeColors[name] = $"#{val}";
                }
            }

            string[] indexMap = ["dk1", "lt1", "dk2", "lt2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink"];

            int index = 0;

            foreach (var name in indexMap)
            {
                if (themeColors.ContainsKey(name))
                    themeColors[index.ToString()] = themeColors[name];

                index++;
            }

            return themeColors;
        }

        public static string ResolveThemedColor(Cell cellData, Cell inheritedCellData, Dictionary<string, string> themeColors, ColorResolveOption options = null)
        {
            var value = ColorHelper.GetColor(cellData?.Value, options?.ColorPalette);
            var inheritedValue = ColorHelper.GetColor(inheritedCellData?.Value, options?.ColorPalette);
            var formula = cellData?.Formula ?? inheritedCellData?.Formula;
            var token = ExtractThemeToken(formula);
            var quickStyleColor = ResolveQuickStyleColor(options?.QuickStyle, themeColors, options?.ThemeDocument);

            if (!string.IsNullOrEmpty(token) && themeColors != null)
            {
                if (token == "FillColor" || token == "FillColor2" || token == "LineColor")
                {
                    if (!string.IsNullOrEmpty(quickStyleColor))
                        return quickStyleColor;

                    if (token == "LineColor")
                        return themeColors.ContainsKey("dk1") ? themeColors["dk1"] : (value ?? inheritedValue ?? "#000000");

                    return themeColors.ContainsKey("accent1") ? themeColors["accent1"] : (value ?? inheritedValue);
                }

                if (themeColors.ContainsKey(token))
                    return themeColors[token];
            }

            if (!string.IsNullOrEmpty(formula) && Regex.IsMatch(formula, @"THEME") && themeColors != null)
            {
                if (options.Role == "line")
                    return themeColors.ContainsKey("dk1") ? themeColors["dk1"] : (value ?? inheritedValue ?? "#000000");
                if (options.Role == "font")
                    return value ?? inheritedValue ?? (themeColors.ContainsKey("dk1") ? themeColors["dk1"] : null) ?? "#000000";
                if (!string.IsNullOrEmpty(quickStyleColor))
                    return quickStyleColor;
            }

            if (!string.IsNullOrEmpty(value))
                return value;
            if (!string.IsNullOrEmpty(quickStyleColor))
                return quickStyleColor;
            if (!string.IsNullOrEmpty(inheritedValue))
                return inheritedValue;

            return null;
        }

        public static string ExtractThemeToken(string formula)
        {
            if (string.IsNullOrEmpty(formula))
                return null;

            var match = Regex.Match(formula, @"THEMEVAL\s*\(\s*""?([A-Za-z0-9_]+)""?", RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Groups[1].Value;

            var numeric = Regex.Match(formula, @"THEMEVAL\s*\(\s*(\d+)", RegexOptions.IgnoreCase);

            if (numeric.Success)
                return numeric.Groups[1].Value;

            return null;
        }

        public static string ResolveQuickStyleColor(string value, Dictionary<string, string> themeColors, XDocument themeDocument)
        {
            if (themeColors == null || string.IsNullOrEmpty(value))
                return null;

            int n = 0;

            if (!int.TryParse(value, out n))
            {
                return null;
            }

            if (themeDocument != null)
            {
                var variationClrScheme = themeDocument.Root.Child("themeElements")?.Child("clrScheme")?.Child("extLst")?.Descendants()?.FirstOrDefault(item => item.Name.LocalName == "variationClrSchemeLst")?.Child("variationClrScheme");

                if (variationClrScheme != null)
                {
                    XElement varColor = variationClrScheme.Child($"varColor{(n - 100 + 1)}");

                    if (varColor != null)
                    {
                        string color = varColor.Child("srgbClr")?.GetAttributeValue("val");

                        if(!string.IsNullOrEmpty(color))
                        {
                            if(color.Length == 6)
                            {
                                color = "#" + color;

                                return color;
                            }
                        }
                    }
                }
            }

            string name = QUICKSTYLE_COLOR_MAP.ContainsKey(n) ? QUICKSTYLE_COLOR_MAP[n] : null;

            return name != null && themeColors.ContainsKey(name) ? themeColors[name] : null;
        }
    }
}
