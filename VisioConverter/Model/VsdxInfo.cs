namespace VisioConverter.Model
{
    public class VsdxInfo
    {
        public Document Document { get; set; }
        public List<Master> Masters { get; set; }
        public List<Page> Pages { get; set; }
        public Dictionary<string, string> ThemeColors { get; set; }
        public Dictionary<int, string> ColorPalette { get; set; }
    }
}
