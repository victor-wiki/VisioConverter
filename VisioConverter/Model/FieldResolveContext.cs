namespace VisioConverter.Model
{
    public class FieldResolveContext
    {
        public int? PageNumber { get; set; }
        public string PageName { get; set; }
        public Dictionary<string, string> PropertySectinMap { get; set; }
        public Dictionary<string, string> UserSectionMap { get; set; }
        public List<dynamic> Fields { get; set; }
    }
}
