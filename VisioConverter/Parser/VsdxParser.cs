using System.Globalization;
using System.IO.Packaging;
using System.Xml.Linq;
using VisioConverter.Extension;
using VisioConverter.Helper;
using VisioConverter.Model;
using VisioConverter.Parser;
using VsdxEditorSharp.Model;
using Document = VisioConverter.Model.Document;

namespace VisioConverter
{
    public partial class VsdxParser
    {
        private string filePath;
        private Stream stream;
        private Package package;
        private PackagePartCollection packageParts;

        public VsdxParser(string filePath)
        {
            this.filePath = filePath;
        }

        public VsdxParser(Stream stream)
        {
            this.stream = stream;
        }       

        public VsdxInfo Parse()
        {
            VsdxInfo vsdxInfo = new VsdxInfo();

            using (Stream stream = !string.IsNullOrEmpty(filePath)? new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read): this.stream)
            {
                var package = Package.Open(stream, FileMode.Open, FileAccess.Read);

                this.package = package;

                var parts = package.GetParts();

                this.packageParts = parts;

                var themePart = GetPackagePart("/visio/theme/theme1.xml") ?? GetPackagePart("/visio/theme/theme2.xml");
                var documentPart = this.GetPackagePart("/visio/document.xml");
                var mastersPart = this.GetPackagePart("/visio/masters/masters.xml");
                var pagesPart = this.GetPackagePart("/visio/pages/pages.xml");
                var mediaParts = packageParts.Where(item => item.Uri.OriginalString.StartsWith("/visio/media/"));

                Document document = null;
                XDocument themeDocument = null;
                List<Master> masters = new List<Master>();
                List<Page> pages = new List<Page>();
                var medias = new List<MediaInfo>();

                var colorPalette = ColorHelper.ColorPalette.ToDictionary();
                var themeColors = new Dictionary<string, string>();

                if (themePart != null)
                {
                    themeDocument = XDocument.Parse(GetFileContent(themePart));
                    themeColors = StyleHelper.GetThemeColors(themeDocument);
                }

                if (mediaParts != null)
                {
                    foreach (var mp in mediaParts)
                    {
                        string key = mp.Uri.OriginalString;
                        string filename = Path.GetFileName(key);
                        byte[] bytes = GetFileBytes(mp);

                        medias.Add(new MediaInfo()
                        {
                            FileName = filename,
                            Bytes = bytes,
                            DataUri = FileHelper.GetImageBase64String(bytes, filename)
                        });
                    }
                }

                if (documentPart != null)
                {
                    string documentsXml = this.GetFileContent(documentPart);

                    document = this.ParseDocument(documentsXml);

                    if (document.HasColor)
                    {
                        foreach (var color in document.Colors)
                        {
                            colorPalette.Add(int.Parse(color.Key), color.Value);
                        }
                    }
                }

                if (mastersPart != null)
                {
                    masters = this.ParseMasters(mastersPart);
                }

                if (pagesPart != null)
                {
                    pages = this.ParsePages(pagesPart, document, masters, themeDocument, themeColors, colorPalette, medias);
                }

                vsdxInfo.Document = document;
                vsdxInfo.Masters = masters;
                vsdxInfo.Pages = pages;
                vsdxInfo.ThemeColors = themeColors;
                vsdxInfo.ColorPalette = colorPalette;
            }

            return vsdxInfo;
        }

        private Document ParseDocument(string xml)
        {
            XDocument xmlDoc = XDocument.Parse(xml);

            Document doc = new Document();

            var root = xmlDoc.Root;

            var styleSheets = root.Child("StyleSheets")?.Children("StyleSheet");

            if (styleSheets != null)
            {
                doc.StyleSheets = new List<StyleSheet>();

                foreach (var ss in styleSheets)
                {
                    string sid = ss.GetAttributeValue("ID");

                    if (string.IsNullOrEmpty(sid))
                    {
                        continue;
                    }

                    var cells = GetCells(ss);                   

                    StyleSheet styleSheet = new StyleSheet()
                    {
                        Id = sid,
                        Cells = cells,
                        LineStyle = ss.GetAttributeValue("LineStyle"),
                        FillStyle = ss.GetAttributeValue("FillStyle"),
                        TextStyle = ss.GetAttributeValue("TextStyle")
                    };

                    doc.StyleSheets.Add(styleSheet);
                }
            }

            var colors = root.Child("Colors")?.Children("ColorEntry");

            if (colors != null)
            {
                doc.Colors = new Dictionary<string, string>();

                foreach (var color in colors)
                {
                    doc.Colors.Add(color.GetAttributeValue("IX"), color.GetAttributeValue("RGB"));
                }
            }

            return doc;
        }

        private List<Master> ParseMasters(PackagePart part)
        {
            string xml = this.GetFileContent(part);
            XDocument xmlDoc = XDocument.Parse(xml);

            List<Master> masters = new List<Master>();

            var relationships = part.GetRelationships().ToList();

            var masterElements = xmlDoc.Root.Children("Master");

            foreach (var element in masterElements)
            {
                var id = element.GetAttributeValue("ID");
                var name = element.GetAttributeValue("Name");
                var rel = element.Child("Rel");
                var rid = rel != null ? rel.GetAttributeValue("id") : null;

                var relationship = rid != null ? relationships.FirstOrDefault(item => item.Id == rid) : null;

                var target = relationship != null ? relationship.TargetUri.OriginalString : null;

                if (!string.IsNullOrEmpty(target))
                {
                    var masterPath = "/visio/masters/" + target;

                    var masterPart = this.GetPackagePart(masterPath);

                    var masterContent = this.GetFileContent(masterPart);

                    if (!string.IsNullOrEmpty(masterContent))
                    {
                        masters.Add(new Master { Id = id, Name = name, Shapes = this.ParseMasterShapes(masterContent) });
                    }
                }
            }

            return masters;
        }

        private List<Shape> ParseMasterShapes(string xml)
        {
            List<Shape> shapes = new List<Shape>();

            var document = XDocument.Parse(xml);

            var elements = document.Root.Descendants().Where(item => item.Name.LocalName == "Shape").ToArray();

            foreach (var element in elements)
            {
                var id = element.GetAttributeValue("ID");

                var shape = new Shape()
                {
                    Id = id,
                    FillStyle = element.GetAttributeValue("FillStyle"),
                    LineStyle = element.GetAttributeValue("LineStyle"),
                    TextStyle = element.GetAttributeValue("TextStyle"),
                    Cells = GetCells(element),
                    Element = element
                };

                shapes.Add(shape);
            }

            return shapes;
        }

        private List<Page> ParsePages(PackagePart part, Document document, List<Master> masters, XDocument themeDocument, Dictionary<string, string> themeColors, Dictionary<int, string> colorPalette, List<MediaInfo> medias)
        {
            string xml = this.GetFileContent(part);
            XDocument xmlDoc = XDocument.Parse(xml);

            List<Page> pages = new List<Page>();

            var relationships = part.GetRelationships().ToList();

            var pageElements = xmlDoc.Root.Children("Page");

            int i = 0;

            foreach (var element in pageElements)
            {
                bool isCustomName = element.GetAttributeValue("IsCustomName") == "1";

                if (isCustomName)
                {
                    continue;
                }

                var id = element.GetAttributeValue("ID");
                var name = element.GetAttributeValue("Name");
                var isBackground = element.GetAttributeValue("Background") == "1";

                var pageSheet = element.Child("PageSheet");
                var pageWidth = pageSheet != null ? GetCellFloatValue(pageSheet, "PageWidth") : null;
                var pageHeight = pageSheet != null ? GetCellFloatValue(pageSheet, "PageHeight") : null;

                var drawingUnitInInches = 1.0f;
                var drawingScale = 1.0f;

                if (pageSheet != null)
                {
                    var pwCell = pageSheet.Child("PageWidth");
                    var u = pwCell != null ? pwCell.GetAttributeValue("U") : null;

                    if (u == "MM")
                        drawingUnitInInches = 1 / 25.4f;
                    else if (u == "CM")
                        drawingUnitInInches = 1 / 2.54f;
                    else if (u == "M")
                        drawingUnitInInches = 1 / 0.0254f;
                    else if (u == "PT")
                        drawingUnitInInches = 1 / 72.0f;
                    else if (u == "FT" || u == "FT_C")
                        drawingUnitInInches = 12.0f;

                    var pageScale = GetCellFloatValue(pageSheet, "PageScale");
                    var drawingScaleValue = GetCellFloatValue(pageSheet, "DrawingScale");

                    if (pageScale.HasValue && drawingScaleValue.HasValue && pageScale > 0 && drawingScaleValue > 0)
                    {
                        drawingScale = drawingScaleValue.Value / pageScale.Value;
                    }
                }

                var backPage = pageSheet != null ? GetCellValue(pageSheet, "BackPage") : null;

                var layers = new List<Layer>();
                var connects = new List<Connect>();
                var shapes = new List<Shape>();

                if (pageSheet != null)
                {
                    var layerSections = pageSheet.Children("Section").Where(s => s.GetAttributeValue("N") == "Layer").ToArray();

                    if (layerSections.Length > 0)
                    {
                        var layerRows = layerSections[0].Children("Row");

                        foreach (var row in layerRows)
                        {
                            var ix = row.GetAttributeValue("IX");
                            var layerName = GetCellValue(row, "Name") ?? GetCellValue(row, "NameUniv");
                            var visible = GetCellValue(row, "Visible");
                            var print = GetCellValue(row, "Print");
                            var active = GetCellValue(row, "Active");
                            var _lock = GetCellValue(row, "Lock");
                            var snap = GetCellValue(row, "Snap");
                            var glue = GetCellValue(row, "Glue");

                            layers.Add(new Layer()
                            {
                                Index = ix,
                                Name = layerName,
                                NameUniv = GetCellValue(row, "NameUniv"),
                                Visible = visible != "0",
                                Print = print != "0",
                                Active = active == "1",
                                Lock = _lock == "1",
                                Snap = snap != "0",
                                Glue = glue != "0",
                                Color = ColorHelper.GetColor(GetCellValue(row, "Color"), colorPalette) ?? GetCellValue(row, "Color"),
                                ColorTrans = GetCellValue(row, "ColorTrans"),
                                CellInfo = new LayerCellInfo()
                                {
                                    Visible = visible,
                                    Print = print,
                                    Active = active,
                                    Lock = _lock,
                                    Snap = snap,
                                    Glue = glue,
                                    Color = GetCellValue(row, "Color"),
                                    ColorTrans = GetCellValue(row, "ColorTrans")
                                }
                            });
                        }
                    }
                }

                var rel = element.Child("Rel");
                var rid = rel != null ? rel.GetAttributeValue("id") : null;

                var relationship = rid != null ? relationships.FirstOrDefault(item => item.Id == rid) : null;

                var target = relationship != null ? relationship.TargetUri.OriginalString : null;

                if (!string.IsNullOrEmpty(target))
                {
                    var pagePath = "/visio/pages/" + target;
                    var pagePart = this.GetPackagePart(pagePath);

                    if (pagePart != null)
                    {
                        var pageContent = this.GetFileContent(pagePart);

                        var pageDoc = XDocument.Parse(pageContent);

                        var connectsElements = pageDoc.Root.Child("Connects").Children("Connect");

                        if(connectsElements!=null)
                        {
                            foreach (var connEl in connectsElements)
                            {
                                connects.Add(new Connect()
                                {
                                    FromSheet = connEl.GetAttributeValue("FromSheet"),
                                    ToSheet = connEl.GetAttributeValue("ToSheet"),
                                    FromCell = connEl.GetAttributeValue("FromCell"),
                                    ToCell = connEl.GetAttributeValue("ToCell"),
                                    FromPart = connEl.GetAttributeValue("FromPart"),
                                    ToPart = connEl.GetAttributeValue("ToPart"),
                                });
                            }
                        }                        

                        var pageShapes = pageDoc.Root.Child("Shapes");

                        if (pageShapes != null)
                        {
                            var shapeElements = pageShapes.Children("Shape");

                            foreach (var shapeElement in shapeElements)
                            {
                                shapes.Add(this.ParseShape(shapeElement, masters, null, themeDocument, themeColors, colorPalette, document.StyleSheets, medias, relationships));
                            }
                        }                        
                    }
                }

                var resolveCtx = new FieldResolveContext() { PageName = name, PageNumber = i + 1 };

                Action<Shape> applyFields = null;

                applyFields = (shape) =>
                {
                    var ctx = resolveCtx;
                    ctx.PropertySectinMap = shape.PropertySectionMap;
                    ctx.UserSectionMap = shape.UserSectionMap;
                    ctx.Fields = shape.Fields;

                    if (!string.IsNullOrEmpty(shape.Text))
                        shape.Text = ShapeInheritance.ResolveFields(shape, ctx);
                   
                    shape.Fields = null;

                    if (shape.HasSubShape)
                    {
                        foreach (var child in shape.SubShapes)
                            applyFields(child);
                    }
                };

                foreach (var sh in shapes)
                    applyFields(sh);

                pages.Add(new Page()
                {
                    Id = id,
                    Name = name,
                    Width = pageWidth ?? 8.5f,
                    Height = pageHeight ?? 11.0f,
                    DrawingUnitInInches = drawingUnitInInches,
                    DrawingScale = drawingScale,
                    IsBackground = isBackground,
                    BackPage = backPage,
                    Layers = layers,
                    Shapes = shapes,
                    Connects = connects,
                    ThemeColors = themeColors,
                    Document = document
                });

                i++;
            }

            return pages;
        }       
    }
}
