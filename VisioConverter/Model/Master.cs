using System.Xml.Linq;

namespace VisioConverter.Model
{
    public class Master
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Shape> Shapes { get; set; }

        public bool HasShape => this.Shapes != null && this.Shapes.Count > 0;
    }
}
