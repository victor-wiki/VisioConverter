using System.Xml.Linq;

namespace VisioConverter.Model
{
    public class ColorResolveOption
    {
        public string QuickStyle { get; set; }
        public Dictionary<int, string> ColorPalette { get; set; }
        public string Role { get; set; }
        public XDocument ThemeDocument { get; set; }
    }
}
