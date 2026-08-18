namespace VisioConverter.Model
{
    public class Layer
    {
        public string Index { get; set; }
        public string Name { get; set; }
        public string NameUniv { get; set; }
        public bool Visible { get; set; }
        public bool Print { get; set; }
        public bool Active { get; set; }
        public bool Lock { get; set; }
        public bool Snap { get; set; }
        public bool Glue { get; set; }
        public string Color { get; set; }
        public string ColorTrans { get; set; }
        public LayerCellInfo CellInfo { get; set; }
    }
}
