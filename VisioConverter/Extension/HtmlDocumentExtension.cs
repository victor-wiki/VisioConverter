using HtmlAgilityPack;

namespace VisioConverter.Extension
{
    public static class HtmlDocumentExtension
    {
        public static HtmlNode CreateSvg(this HtmlDocument document)
        {
            HtmlNode node = document.CreateElement("svg");

            node.SetAttributeValue("xmlns", "http://www.w3.org/2000/svg");
            node.SetAttributeValue("xmlns:v", "http://schemas.microsoft.com/visio/2003/SVGExtensions/");
            node.SetAttributeValue("xmlns:xlink", "http://www.w3.org/1999/xlink");

            return node;
        }
    }
}
