using System.Xml.Linq;

namespace VisioConverter.Model
{
    public class ParagraphInfo
    {
        public string Text { get; set; }
        public List<TextInfo> Runs { get; set; }
        public List<ParagraphFieldInfo> Fields { get; set; }
    }

    public class ParagraphFieldInfo
    {
        public int? Index { get; set; }
        public XElement Element { get; set; }
    }
}
