using VsdxEditorSharp.Model;

namespace VisioConverter.Model
{
    public class Page
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float? DrawingUnitInInches { get; set; }
        public float? DrawingScale { get; set; }
        public bool IsBackground { get; set; }
        public string BackPage { get; set; }
        public List<Layer> Layers { get; set; }
        public List<Shape> Shapes { get; set; }
        public List<Connect> Connects { get; set; }
        public Dictionary<string, string> ThemeColors { get; set; }
        public Document Document { get; set; }
    }
}
