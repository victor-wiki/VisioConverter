namespace VisioConverter.Model
{
    public class ConvertResult
    {
        public bool IsOK => this.Infos?.All(item => item.IsOK) == true;

        public List<HtmlConvertInfo> Infos { get; set; }
        public string Message
        {
            get
            {
                return string.Join(Environment.NewLine, this.Infos?.Where(item => item.IsOK == false).Select(item => $"Slide{(item.Number)}:{item.Message}"));
            }
        }
    }
}
