namespace VisioConverter.Model
{
    public class Row
    {
        public string Index { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsDelete { get; set; }
        public List<Cell> Cells { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public dynamic? A { get; set; }
        public float? B { get; set; }
        public float? C { get; set; }
        public float? D { get; set; }
        public dynamic E { get; set; }
    }
}
