using NaturalSort.Extension;
using System.IO.Packaging;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using VisioConverter.Extension;
using VisioConverter.Helper;
using VisioConverter.Model;
using VisioConverter.Parser;

namespace VisioConverter
{
    public partial class VsdxParser
    {
        private Shape ParseShape(XElement shapeElement, List<Master> masters, Master parentMaster,XDocument themeDocument, Dictionary<string, string> themeColors, Dictionary<int, string> colorPalette, List<StyleSheet> styleSheets, List<MediaInfo> medias, List<PackageRelationship> pageRelationships)
        {
            var id = shapeElement.GetAttributeValue("ID");
            var name = shapeElement.GetAttributeValue("Name");
            var nameU = shapeElement.GetAttributeValue("NameU");
            var masterId = shapeElement.GetAttributeValue("Master");
            var masterShapeId = shapeElement.GetAttributeValue("MasterShape");
            var type = shapeElement.GetAttributeValue("Type");

            var master = !string.IsNullOrEmpty(masterId) ? masters.FirstOrDefault(item=>item.Id == masterId) : null;
            Shape masterShape = null;

            if (master != null)
            {
                if (!string.IsNullOrEmpty(masterShapeId) && master.HasShape)
                {
                    masterShape = master.Shapes.FirstOrDefault(item=>item.Id == masterShapeId);
                }
                else if (master.HasShape)
                {
                    masterShape = master.Shapes[0];
                }
            }
            else if (!string.IsNullOrEmpty(masterShapeId) && parentMaster != null && parentMaster.HasShape != null)
            {
                masterShape = parentMaster.Shapes.FirstOrDefault(item => item.Id == masterShapeId);
                master = parentMaster;
            }

            var pinX = GetMergedCellFloatValue(shapeElement, masterShape, "PinX");
            var pinY = GetMergedCellFloatValue(shapeElement, masterShape, "PinY");
            var width = GetMergedCellFloatValue(shapeElement, masterShape, "Width");
            var height = GetMergedCellFloatValue(shapeElement, masterShape, "Height");
            var locPinX = GetMergedCellFloatValue(shapeElement, masterShape, "LocPinX", width / 2.0f);
            var locPinY = GetMergedCellFloatValue(shapeElement, masterShape, "LocPinY", height / 2.0f);
            var txtPinX = GetMergedCellFloatValue(shapeElement, masterShape, "TxtPinX", width / 2.0f);
            var txtPinY = GetMergedCellFloatValue(shapeElement, masterShape, "TxtPinY", height / 2.0f);
            var txtWidth = GetMergedCellFloatValue(shapeElement, masterShape, "TxtWidth", width);
            var txtHeight = GetMergedCellFloatValue(shapeElement, masterShape, "TxtHeight", height);
            var angle = GetMergedCellFloatValue(shapeElement, masterShape, "Angle");
            var flipX = GetMergedCellValue(shapeElement, masterShape, "FlipX");
            var flipY = GetMergedCellValue(shapeElement, masterShape, "FlipY");
            var beginX = GetCellFloatValue(shapeElement, "BeginX") ?? (masterShape != null ? GetCellFloatValue(masterShape, "BeginX") : null);
            var beginY = GetCellFloatValue(shapeElement, "BeginY") ?? (masterShape != null ? GetCellFloatValue(masterShape, "BeginY") : null);
            var endX = GetCellFloatValue(shapeElement, "EndX") ?? (masterShape != null ? GetCellFloatValue(masterShape, "EndX") : null);
            var endY = GetCellFloatValue(shapeElement, "EndY") ?? (masterShape != null ? GetCellFloatValue(masterShape, "EndY") : null);
            var objType = GetMergedCellValue(shapeElement, masterShape, "ObjType");
            var quickStyleLineColor = GetMergedCellValue(shapeElement, masterShape, "QuickStyleLineColor");
            var quickStyleFillColor = GetMergedCellValue(shapeElement, masterShape, "QuickStyleFillColor");
            var quickStyleFontColor = GetMergedCellValue(shapeElement, masterShape, "QuickStyleFontColor");
            var is1D = (beginX != null && endX != null) || objType == "2";

            var lineStyleId = shapeElement.GetAttributeValue("LineStyle") ?? (masterShape != null ? masterShape.LineStyle : null);
            var fillStyleId = shapeElement.GetAttributeValue("FillStyle") ?? (masterShape != null ? masterShape.FillStyle : null);      

            var lineColorData = GetCell(shapeElement, "LineColor");
            var masterLineColorData = masterShape != null ? GetCell(masterShape, "LineColor") : null;
            var styleLineColorData = ResolveStyleCellData(styleSheets, lineStyleId, "LineColor", "line");
            var lineColor = StyleHelper.ResolveThemedColor(lineColorData, masterLineColorData ?? styleLineColorData, themeColors, new ColorResolveOption()
            {
                Role = "line",
                QuickStyle = quickStyleLineColor,
                ColorPalette = colorPalette
            }) ?? "#000000";

            float lineWeight = GetMergedCellFloatValue(shapeElement, masterShape , "LineWeight", StyleCellFloat(styleSheets, lineStyleId, "LineWeight", "line") ?? 0.01f);
            var linePattern = GetMergedCellFloatValue(shapeElement, masterShape, "LinePattern", StyleCellFloat(styleSheets, lineStyleId, "LinePattern", "line") ?? 1f);
            var fillForegroundData = GetCell(shapeElement, "FillForegnd");
            var masterFillForegroundData = masterShape != null ? GetCell(masterShape, "FillForegnd") : null;                      

            var styleFillForegroundData = ResolveStyleCellData(styleSheets, fillStyleId, "FillForegnd", "fill");        

            var fillForeground = StyleHelper.ResolveThemedColor(fillForegroundData, masterFillForegroundData ?? styleFillForegroundData, themeColors, new ColorResolveOption()
            {
                Role = "fill",
                QuickStyle = quickStyleFillColor,
                ColorPalette = colorPalette,
                ThemeDocument = themeDocument
            });

            var fillBackgroundData = GetCell(shapeElement, "FillBkgnd");
            var masterFillBackgroundData = masterShape != null ? GetCell(masterShape, "FillBkgnd") : null;
            var styleFillBackgroundData = ResolveStyleCellData(styleSheets, fillStyleId, "FillBkgnd", "fill");
            var fillBackground = StyleHelper.ResolveThemedColor(fillBackgroundData, masterFillBackgroundData ?? styleFillBackgroundData, themeColors, new ColorResolveOption()
            {
                Role = "fill",
                QuickStyle = quickStyleFillColor,
                ColorPalette = colorPalette
            });

            var fillForegroundTrans = GetMergedCellFloatValue(shapeElement, masterShape, "FillForegndTrans", StyleCellFloat(styleSheets, fillStyleId, "FillForegndTrans", "fill") ?? 0);
            var fillBackgroundTrans = GetMergedCellFloatValue(shapeElement, masterShape, "FillBkgndTrans", StyleCellFloat(styleSheets, fillStyleId, "FillBkgndTrans", "fill") ?? fillForegroundTrans);
            var fillPattern = GetMergedCellFloatValue(shapeElement, masterShape, "FillPattern", StyleCellFloat(styleSheets, fillStyleId, "FillPattern", "fill")?? 1);
            var fillGradientDir = GetCellFloatValue(shapeElement, "FillGradientDir") ?? (masterShape != null ? GetCellFloatValue(masterShape.Cells, "FillGradientDir") : null);
            var shapeGradientStops = ParseFillGradientStops(shapeElement, themeColors, colorPalette);
            var masterGradientStops = masterShape != null ? ParseFillGradientStops(masterShape.Element, themeColors, colorPalette) : [];
            var fillGradientStops = shapeGradientStops.Count > 0 ? shapeGradientStops : masterGradientStops;
            var rounding = GetMergedCellFloatValue(shapeElement, masterShape, "Rounding");
            var beginArrow = GetMergedCellFloatValue(shapeElement, masterShape, "BeginArrow");
            var endArrow = GetMergedCellFloatValue(shapeElement, masterShape, "EndArrow");
            var imgOffsetX = GetMergedCellFloatValue(shapeElement, masterShape, "ImgOffsetX");
            var imgOffsetY = GetMergedCellFloatValue(shapeElement, masterShape, "ImgOffsetY");
            var imgWidth = GetMergedCellFloatValue(shapeElement, masterShape, "ImgWidth");
            var imgHeight = GetMergedCellFloatValue(shapeElement, masterShape, "ImgHeight");
  
            var characterSections = shapeElement.Children("Section").Where(s => s.GetAttributeValue("N") == "Character").Select(item => GetSection(item)).ToArray();
            var charFormats = this.ParseCharacterFormats(shapeElement, themeColors, quickStyleFontColor, colorPalette);
            float? fontSize = null;
            string fontColor = null;
            string fontFamily = null;
            var bold = false;
            var italic = false;

            if (characterSections.Length > 0)
            {
                var charRows = characterSections[0].Rows;

                if (charRows.Count > 0)
                {
                    fontSize = GetCellFloatValue(charRows[0], "Size");
                    fontFamily = GetCellValue(charRows[0], "Font") ?? GetCellValue(charRows[0], "ComplexScriptFont") ?? GetCellValue(charRows[0], "AsianFont");
                    fontColor = StyleHelper.ResolveThemedColor(GetCell(charRows[0], "Color"), null, themeColors, new ColorResolveOption()
                    {
                        Role = "font",
                        QuickStyle = quickStyleFontColor,
                        ColorPalette = colorPalette
                    });

                    var style = GetCellValue(charRows[0], "Style");

                    if (!string.IsNullOrEmpty(style) && int.TryParse(style, out _))
                    {
                        var styleNum = Convert.ToInt32(style);
                        bold = (styleNum & 1) != 0;
                        italic = (styleNum & 2) != 0;
                    }
                }
            }

            if (masterShape != null && fontSize == null)
            {
                var mCharSections = masterShape.Element.Children("Section")?.Where(s => s.GetAttributeValue("N") == "Character")?.Select(item => GetSection(item))?.ToArray();

                if (mCharSections != null && mCharSections.Length > 0)
                {
                    var mCharRows = mCharSections[0].Rows;

                    if (mCharRows.Count > 0)
                    {
                        fontSize = fontSize ?? GetCellFloatValue(mCharRows[0], "Size");
                        fontFamily = fontFamily ?? GetCellValue(mCharRows[0], "Font") ?? GetCellValue(mCharRows[0], "ComplexScriptFont") ?? GetCellValue(mCharRows[0], "AsianFont");
                        fontColor = fontColor ?? StyleHelper.ResolveThemedColor(GetCell(mCharRows[0], "Color"), null, themeColors, new ColorResolveOption()
                        {
                            Role = "font",
                            QuickStyle = quickStyleFontColor,
                            ColorPalette = colorPalette
                        });
                    }
                }
            }

            if (string.IsNullOrEmpty(fillForeground) && fillPattern != 0)
            {
                if (masterShape != null && styleSheets != null && colorPalette != null)
                {
                    string masterFillStyleId = masterShape.FillStyle;

                    if (!string.IsNullOrEmpty(masterFillStyleId))
                    {
                        StyleSheet styleSheet = styleSheets.FirstOrDefault(item => item.Id == masterFillStyleId);

                        if (styleSheet != null)
                        {
                            var referencedStyleSheet = styleSheets.FirstOrDefault(item => item.Id == styleSheet.FillStyle && item.Id != "0");

                            if (referencedStyleSheet != null)
                            {
                                styleSheet = referencedStyleSheet;
                            }
                        }

                        var value = GetCellValue(styleSheet.Cells, "FillForegnd");
                        var formula = GetCellFormula(styleSheet.Cells, "FillForegnd");

                        if (value == "Themed" || formula == "THEMEVAL()")
                        {
                            if (themeColors.ContainsKey("accent1"))
                            {
                                fillForeground = themeColors["accent1"];
                            }
                        }
                    }
                }
            }
            
            var layerMemberRaw = GetCellValue(shapeElement, "LayerMember") ?? (masterShape != null ? GetCellValue(masterShape.Cells, "LayerMember") : null);
            var layerMembers = !string.IsNullOrEmpty(layerMemberRaw)
              ? layerMemberRaw.Split(";").Select(s => s.Trim()).Where(item => item != "0" && item.ToLower() != "false").ToArray()
              : [];
            
            var geometry = this.MergeGeometry(masterShape?.Element ?? null, shapeElement, is1D);

            var hasGeometry = this.HasGeometrySections(shapeElement) || this.HasGeometrySections(masterShape?.Element ?? null);
    
            var subShapes = new List<Shape>();
            var shapesContainer = shapeElement.Child("Shapes");

            if (shapesContainer != null)
            {
                var childShapeElements = shapesContainer.Children("Shape");

                foreach (var childEl in childShapeElements)
                {
                    subShapes.Add(this.ParseShape(childEl, masters, master, themeDocument, themeColors, colorPalette, styleSheets, medias, pageRelationships));
                }
            }

            var p = this.GetTextContent(shapeElement);

            string rawText = p?.Text;

            List<ParagraphFieldInfo> inlineFields = p?.Fields;
            var rawTextRuns = p?.Runs;
            
            var shapeFields = this.ParseFieldSection(shapeElement);
            var masterFields = masterShape != null ? this.ParseFieldSection(masterShape.Element) : new Dictionary<string, FieldSectionInfo>();
            var fieldTable = shapeFields.Count > 0 ? shapeFields : masterFields;
         
            var orderedFields = new List<dynamic>();

            if (inlineFields != null)
            {
                foreach (var f in inlineFields)
                {
                    if (f.Index != null && fieldTable.ContainsKey(f.Index.ToString()))
                        orderedFields.Add(fieldTable[f.Index.ToString()]);
                    else
                        orderedFields.Add(f);
                }
            }
           
            var masterPropertySectionMap = masterShape != null ? this.ParsePropertySection(masterShape.Element) : new Dictionary<string, string>();
            var shapePropertySectionMap = this.ParsePropertySection(shapeElement);

            var propertySectionMap = this.MergeMap(masterPropertySectionMap, shapePropertySectionMap);

            var masterUserSectionMap = masterShape != null ? this.ParseUserSection(masterShape.Element) : new Dictionary<string, string>();
            var shapeUserSectionMap = this.ParseUserSection(shapeElement);

            var userSectionMap = this.MergeMap(masterUserSectionMap, shapeUserSectionMap);

            var customProps = this.MergeMetadataRows(masterShape != null ? this.ParseCustomProperties(masterShape.Element) : new List<CustomPropertyInfo>(), this.ParseCustomProperties(shapeElement));

            var masterUerDefs = masterShape != null ? this.ParseUserDefs(masterShape.Element) : new List<UserDef>();
            var shapeUserDefs = this.ParseUserDefs(shapeElement);
            var userDefs = Enumerable.Concat(masterUerDefs, shapeUserDefs).ToList();

            var title = name ?? nameU ?? (!string.IsNullOrEmpty(master?.Name) && !string.IsNullOrEmpty(id) ? $"{master.Name}.{id}" : null) ?? (string.IsNullOrEmpty(id) ? $"{type ?? "Shape"}.{id}" : null);
            var foreignData = this.ParseForeignData(shapeElement) ?? (masterShape != null ? this.ParseForeignData(masterShape.Element) : null);
            var image = this.ResolveImageData(foreignData, pageRelationships, medias);

            if (image != null)
            {
                image.X = imgOffsetX;
                image.Y = imgOffsetY;
                image.Width = imgWidth;
                image.Height = imgHeight;
            }

            var shape = new Shape()
            {
                Id = id,
                Name = name,
                NameU = nameU,
                Title = title,
                MasterId = masterId,
                MasterShapeId = masterShapeId,
                Type = type,
                Cells = GetCells(shapeElement),
                PinX = pinX,
                PinY = pinY,
                Width = width,
                Height = height,
                LocPinX = locPinX,
                LocPinY = locPinY,
                TextPinX = txtPinX,
                TextPinY = txtPinY,
                TextWidth = txtWidth,
                TextHeight = txtHeight,
                Angle = angle,
                FlipX = flipX == "1",
                FlipY = flipY == "1",
                LineColor = lineColor,
                LineWeight = lineWeight,
                LinePattern = linePattern,
                FillForeground = fillForeground,
                FillBackground = fillBackground,
                FillForegroundTrans = fillForegroundTrans,
                FillBackgroundTrans = fillBackgroundTrans,
                FillPattern = fillPattern,
                FillGradientDir = fillGradientDir,
                FillGradientStops = fillGradientStops,
                Image = image,
                Rounding = rounding,
                BeginArrow = beginArrow,
                EndArrow = endArrow,
                BeginX = beginX,
                BeginY = beginY,
                EndX = endX,
                EndY = endY,
                ObjectType = objType,
                Is1D = is1D,
                FontSize = fontSize,
                FontFamily = fontFamily,
                FontColor = fontColor,
                Bold = bold,
                Italic = italic,
                CharacterFormats = charFormats,
                Geometries = geometry,
                SubShapes = subShapes,
                Text = rawText,
                LayerMembers = layerMembers,
                PropertySectionMap = propertySectionMap,
                UserSectionMap = userSectionMap,
                CustomPropertyInfos = customProps,
                UserDefs = userDefs,
                StyleInfo = new StyleInfo()
                {
                    LineColorFormula = lineColorData?.Formula ?? masterLineColorData?.Formula,
                    FillForegroundFormula = fillForegroundData?.Formula ?? masterFillForegroundData?.Formula,
                    FillBackgroundFormula = fillBackgroundData?.Formula ?? masterFillBackgroundData?.Formula,
                    GuickStyleLineColor = quickStyleLineColor,
                    QuickStyleFillColor = quickStyleFillColor,
                    QuickStyleFontColor = quickStyleFontColor
                },
                Fields = orderedFields,
                Element = shapeElement
            };

            if (rawTextRuns != null)
            {
                shape.TextRuns ??= new List<TextInfo>();

                foreach (var run in rawTextRuns)
                {
                    if (charFormats.ContainsKey(run.CP))
                    {
                        run.Font = charFormats[run.CP];
                    }
                    else if (charFormats.ContainsKey("0"))
                    {
                        run.Font = charFormats["0"];
                    }

                    shape.TextRuns.Add(run);
                }
            }
        
            if (masterShape != null)
            {
                var masterText = this.SerializeTextWithFields(masterShape.Element);

                var masterInherit = new Shape()
                {
                    Text = masterText.Text.Replace("\n", ""),

                    Fields = new List<dynamic>()
                };

                foreach (var f in masterText.Fields)
                {
                    if (f.Index != null && masterFields.ContainsKey(f.Index.Value.ToString()))
                    {
                        masterInherit.Fields.Add(masterFields[f.Index.Value.ToString()]);
                    }
                    else
                    {
                        masterInherit.Fields.Add(f);
                    }
                }
              
                var mCharSections = masterShape.Element.Children("Section").Where(s => s.GetAttributeValue("N") == "Character").ToArray();

                if (mCharSections.Length > 0)
                {
                    var mCharRows = mCharSections[0].Children("Row");

                    if (mCharRows.Count > 0)
                    {
                        masterInherit.FontSize = GetCellFloatValue(mCharRows[0], "Size");
                        masterInherit.FontFamily = GetCellValue(mCharRows[0], "Font") ?? GetCellValue(mCharRows[0], "ComplexScriptFont") ?? GetCellValue(mCharRows[0], "AsianFont");
                        masterInherit.FontColor = StyleHelper.ResolveThemedColor(GetCell(mCharRows[0], "Color"), null, themeColors, new ColorResolveOption()
                        {
                            Role = "font",
                            QuickStyle = quickStyleFontColor,
                            ColorPalette = colorPalette
                        });

                        var style = GetCellValue(mCharRows[0], "Style");

                        if (!string.IsNullOrEmpty(style) && int.TryParse(style, out _))
                        {
                            var sNum = Convert.ToInt32(style);

                            masterInherit.Bold = Convert.ToBoolean(sNum & 1);
                            masterInherit.Italic = Convert.ToBoolean(sNum & 2);
                        }
                    }
                }

                ShapeInheritance.InheritFromMaster(shape, masterInherit);
            }

            return shape;
        }

        private static string GetMergedCellValue(XElement shapeElement, Shape masterShape, string name, string defaultValue = null)
        {
            return GetCellValue(shapeElement, name) ?? (masterShape != null ? GetCellValue(masterShape, name) : null) ?? defaultValue;
        }

        private static float GetMergedCellFloatValue(XElement shapeElement, Shape masterShape, string name, float defaultValue=0)
        {
            return GetCellFloatValue(shapeElement, name) ?? (masterShape != null ? GetCellFloatValue(masterShape, name) : null) ?? defaultValue;
        }

        private static float? StyleCellFloat(List<StyleSheet> styles, string styleId, string cellName, string styleKind)
        {
            var data = ResolveStyleCellData(styles, styleId, cellName, styleKind);

            if (data == null || string.IsNullOrEmpty(data.Value))
                return null;

            if (float.TryParse(data.Value, out var val))
            {
                return val;
            }

            return null;
        }

        private List<GradientStop> ParseFillGradientStops(XElement shapeElement, Dictionary<string, string> themeColors, Dictionary<int, string> colorPalette)
        {
            var sections = shapeElement.Children("Section").Where(item => item.GetAttributeValue("N") == "FillGradientDef").Select(item => GetSection(item)).ToArray();

            if (sections.Length == 0)
                return [];

            var stops = new List<GradientStop>();

            foreach (var sec in sections)
            {
                foreach (var row in sec.Rows)
                {
                    var cell = row.Cells.FirstOrDefault(item => item.Name == "GradientStopPosition");
                    var position = float.TryParse(cell.Value, out var val) ? val : 0;

                    var color = StyleHelper.ResolveThemedColor(cell, null, themeColors, new ColorResolveOption() { Role = "fill", ColorPalette = colorPalette })
                        ?? ColorHelper.GetColor(GetCellValue(row, "GradientStopColor"), colorPalette);
                    var transparency = GetCellNumberValue(row.Cells, "GradientStopTransparency");

                    if (string.IsNullOrEmpty(color))
                        continue;

                    stops.Add(new GradientStop()
                    {
                        Offset = Math.Max(0, Math.Min(100, position * 100)),
                        Color = color,
                        Opacity = Math.Max(0, Math.Min(1, 1 - transparency))
                    });
                }
            }

            return stops.OrderBy(item => item.Position).ToList();
        }

        private Dictionary<string, FontInfo> ParseCharacterFormats(XElement shapeElement, Dictionary<string, string> themeColors, string quickStyleFontColor, Dictionary<int, string> colorPalette)
        {
            var formats = new Dictionary<string, FontInfo>() { };

            var charSections = shapeElement.Children("Section").Where(item => item.GetAttributeValue("N") == "Character").Select(item => GetSection(item));

            foreach (var section in charSections)
            {
                foreach (var row in section.Rows)
                {
                    var ix = row.Index ?? "0";
                    var fontSize = GetCellNumberValue(row.Cells, "Size");
                    var fontColor = StyleHelper.ResolveThemedColor(GetCell(row, "Color"), null, themeColors, new ColorResolveOption()
                    {
                        Role = "font",
                        QuickStyle = quickStyleFontColor,
                        ColorPalette = colorPalette
                    });
                    var fontFamily = GetCellValue(row, "Font") ?? GetCellValue(row, "ComplexScriptFont") ?? GetCellValue(row, "AsianFont");
                    var style = GetCellValue(row, "Style");

                    var styleNum = !string.IsNullOrEmpty(style) && int.TryParse(style, out _) ? Convert.ToInt32(style) : 0;

                    formats[ix] = new FontInfo()
                    {
                        Size = fontSize,
                        Color = fontColor,
                        Family = fontFamily,
                        Bold = Convert.ToBoolean(styleNum & 1),
                        Italic = Convert.ToBoolean(styleNum & 2),
                        Underline = Convert.ToBoolean(styleNum & 4)
                    };
                }
            }

            return formats;
        }

        private List<Section> MergeGeometry(XElement masterElement, XElement shapeElement, bool is1D)
        {
            var masterGeo = masterElement != null ? this.ParseGeometryRaw(masterElement) : new List<Section>();
            var shapeGeo = this.ParseGeometryRaw(shapeElement);
          
            if (shapeGeo.Count == 0 && masterGeo.Count == 0)
                return new List<Section>();

            Func<IEnumerable<Row>, List<Row>> getSortedAndFilteredRows = (rows) =>
            {
                return rows.Where(r => !r.IsDelete).OrderBy(item => item.Index, StringComparison.OrdinalIgnoreCase.WithNaturalSort()).ToList();
            };

            if (shapeGeo.Count == 0)
            {
                return masterGeo.Select(sec => (new Section()
                {
                    Rows = getSortedAndFilteredRows(sec.Rows),
                    NoFill = sec.NoFill ?? false,
                    NoLine = sec.NoLine ?? false,
                    NoShow = sec.NoShow ?? false
                })).ToList();
            }

            if (is1D || masterGeo.Count == 0)
            {
                return shapeGeo.Select(sec => (new Section()
                {
                    Rows = getSortedAndFilteredRows(sec.Rows),
                    NoFill = sec.NoFill ?? false,
                    NoLine = sec.NoLine ?? false,
                    NoShow = sec.NoShow ?? false
                })).ToList();
            }

            var masterByIx = masterGeo.ToDictionary(item => item.Index);

            var merged = new List<Section>();

            // Use shape sections, but fill in missing rows from master
            var seenIx = new HashSet<string>();

            foreach (var shapeSection in shapeGeo)
            {
                seenIx.Add(shapeSection.Index);

                var masterSection = masterByIx.ContainsKey(shapeSection.Index) ? masterByIx[shapeSection.Index] : null;
                var mergedRowMap = new Dictionary<string, Row>();

                if (masterSection != null)
                {
                    foreach (var row in masterSection.Rows)
                    {
                        mergedRowMap.Add(row.Index, row);
                    }
                }
         
                foreach (var row in shapeSection.Rows)
                {
                    mergedRowMap[row.Index] = this.MergeRow(mergedRowMap[row.Index], row);
                }

                var noShow = shapeSection.NoShow ?? masterSection?.NoShow ?? false;

                merged.Add(new Section()
                {
                    Rows = getSortedAndFilteredRows(mergedRowMap.Values),
                    NoFill = shapeSection.NoFill ?? masterSection?.NoFill ?? false,
                    NoLine = shapeSection.NoLine ?? masterSection?.NoLine ?? false,
                    NoShow = noShow
                });
            }
          
            foreach (var masterSec in masterGeo)
            {
                if (!seenIx.Contains(masterSec.Index))
                {
                    merged.Add(new Section()
                    {
                        Rows = getSortedAndFilteredRows(masterSec.Rows),
                        NoFill = masterSec.NoFill ?? false,
                        NoLine = masterSec.NoLine ?? false,
                        NoShow = masterSec.NoShow ?? false
                    });
                }
            }

            return merged;
        }

        private List<Section> ParseGeometryRaw(XElement shapeElement)
        {
            var sections = new List<Section>();
            var sectionEls = shapeElement.Children("Section").Where(item => item.GetAttributeValue("N") == "Geometry");

            foreach (var sec in sectionEls)
            {
                var ix = sec.GetAttributeValue("IX") ?? "0";
                var noFill = GetCellValue(sec, "NoFill");
                var noLine = GetCellValue(sec, "NoLine");
                var noShow = GetCellValue(sec, "NoShow");

                var rows = new List<Row>();
                var rowEls = sec.Children("Row");

                foreach (var row in rowEls)
                {
                    var rowData = ParseRow(row);

                    rows.Add(rowData);
                }

                sections.Add(new Section()
                {
                    Index = ix,
                    Rows = rows,
                    NoFill = this.GetSectionFlag(noFill),
                    NoLine = this.GetSectionFlag(noLine),
                    NoShow = this.GetSectionFlag(noShow)
                });
            }

            return sections;
        }

        private Row MergeRow(Row masterRow, Row shapeRow)
        {
            if (masterRow == null)
                return shapeRow;

            if (shapeRow == null)
                return masterRow;

            var merged = ObjectHelper.CloneObject<Row>(masterRow);

            ObjectHelper.CopyProperties(shapeRow, merged, true);

            merged.IsDelete = shapeRow.IsDelete;

            return merged;
        }

        private bool HasGeometrySections(XElement shapeElement)
        {
            if (shapeElement == null)
                return false;

            return shapeElement.Children("Section")?.Any(sec => sec.GetAttributeValue("N") == "Geometry") == true;
        }

        private ParagraphInfo GetTextContent(XElement shapeElement)
        {
            var textEls = shapeElement.Children("Text");

            if (textEls.Count == 0)
                return null;

            var p = this.SerializeTextWithFields(textEls[0]);

            foreach (var run in p.Runs)
            {
                run.Text = run.Text.Replace("\n", "");
            }

            return new ParagraphInfo
            {
                Text = p.Text.Replace("\n", ""),
                Fields = p.Fields,
                Runs = p.Runs.Where(r => !string.IsNullOrEmpty(r.Text)).ToList()
            };
        }

        private ParagraphInfo SerializeTextWithFields(XElement textEl)
        {
            var _out = "";
            var fields = new List<ParagraphFieldInfo>();
            var runs = new List<TextInfo>();
            var currentCp = "0";
            var currentPp = "0";

            Action<string> appendRun = (text) =>
            {
                if (text == null)
                    return;

                _out += text;

                runs.Add(new TextInfo() { Text = text, CP = currentCp, PP = currentPp });
            };

            Action<XElement> walk = null;

            walk = (node) =>
            {
                var childNodes = node.Nodes().ToList();

                for (var i = 0; i < childNodes.Count; i++)
                {
                    var n = childNodes[i];

                    if (n.NodeType == XmlNodeType.Text)
                    {
                        appendRun((n as XText).Value);
                    }
                    else if (n.NodeType == XmlNodeType.Element)
                    {
                        var el = n as XElement;
                        var name = el.Name.LocalName;

                        if (name == "fld")
                        {
                            // <fld IX="N"/> — IX references a Field section row on this shape.
                            var ix = el.GetAttributeValue("IX");

                            fields.Add(new ParagraphFieldInfo() { Index = ix != null ? Convert.ToInt32(ix) : default(int?), Element = el });

                            appendRun("\uFFFC");
                        }
                        else if (name == "cp")
                        {
                            currentCp = el.GetAttributeValue("IX", "0");
                        }
                        else if (name == "pp")
                        {
                            currentPp = el.GetAttributeValue("IX", "0");
                        }
                        else if (name == "tp")
                        {
                            // Tab properties are formatting-only for now.
                        }
                        else
                        {
                            walk(el);
                        }

                        //if (n.NextNode != null && n.NextNode.NodeType == XmlNodeType.Text)
                        //    appendRun((n.NextNode as XText).Value);
                    }
                }
            };

            walk(textEl);

            return new ParagraphInfo() { Text = _out, Runs = runs, Fields = fields };
        }

        private bool? GetSectionFlag(string value)
        {
            if (value == "1")
                return true;
            if (value == "0")
                return false;

            return null;
        }

        private Row ParseRow(XElement row)
        {
            var type = row.GetAttributeValue("T");
            var ix = row.GetAttributeValue("IX");
            var del = row.GetAttributeValue("Del") == "1";

            var x = GetCellFloatValue(row, "X");

            var y = GetCellFloatValue(row, "Y");
            var rowData = new Row() { Type = type, Index = ix, IsDelete = del, X = x, Y = y };

            if (type == "ArcTo")
            {
                rowData.A = GetCellFloatValue(row, "A");
            }
            else if (type == "EllipticalArcTo" || type == "RelEllipticalArcTo")
            {
                rowData.A = GetCellFloatValue(row, "A");
                rowData.B = GetCellFloatValue(row, "B");
                rowData.C = GetCellFloatValue(row, "C");
                rowData.D = GetCellFloatValue(row, "D");
            }
            else if (type == "NURBSTo")
            {
                rowData.A = GetCellFloatValue(row, "A");
                rowData.B = GetCellFloatValue(row, "B");
                rowData.C = GetCellFloatValue(row, "C");
                rowData.D = GetCellFloatValue(row, "D");
                rowData.E = GetCellFloatValue(row, "E");
            }
            else if (type == "SplineStart")
            {
                rowData.A = GetCellFloatValue(row, "A");
                rowData.B = GetCellFloatValue(row, "B");
                rowData.C = GetCellFloatValue(row, "C");
                rowData.D = GetCellFloatValue(row, "D");
            }
            else if (type == "SplineKnot")
            {
                rowData.A = GetCellFloatValue(row, "A");
            }
            else if (type == "PolylineTo")
            {
                rowData.A = GetCellFloatValue(row, "A");
            }
            else if (type == "InfiniteLine")
            {
                rowData.A = GetCellFloatValue(row, "A");
                rowData.B = GetCellFloatValue(row, "B");
            }
            else if (type == "Ellipse")
            {
                rowData.A = GetCellFloatValue(row, "A");
                rowData.B = GetCellFloatValue(row, "B");
                rowData.C = GetCellFloatValue(row, "C");
                rowData.D = GetCellFloatValue(row, "D");
            }
            else if (type == "RelCubBezTo")
            {
                rowData.A = GetCellFloatValue(row, "A");
                rowData.B = GetCellFloatValue(row, "B");
                rowData.C = GetCellFloatValue(row, "C");
                rowData.D = GetCellFloatValue(row, "D");
            }
            else if (type == "RelQuadBezTo")
            {
                rowData.A = GetCellFloatValue(row, "A");
                rowData.B = GetCellFloatValue(row, "B");
            }

            rowData.Cells ??= GetCells(row);

            return rowData;
        }

        private Dictionary<string, FieldSectionInfo> ParseFieldSection(XElement shapeElement)
        {
            var fields = new Dictionary<string, FieldSectionInfo>();
            var sections = shapeElement.Children("Section").Where(item => item.GetAttributeValue("N") == "Field").Select(item => GetSection(item));

            foreach (var sec in sections)
            {
                var rows = sec.Rows;

                foreach (var row in rows)
                {
                    var ix = row.Index ?? "0";
                    var value = GetCellValue(row, "Value");
                    var format = GetCellValue(row, "Format");

                    // The formula on the Value cell is the actual reference (e.g. Prop.Foo).
                    var cell = row.Cells.FirstOrDefault(item => item.Name == "Value");

                    var _ref = cell?.Formula;

                    fields[ix] = new FieldSectionInfo() { Index = ix, Value = value, Format = format, Ref = _ref };
                }
            }

            return fields;
        }

        private Dictionary<string, string> ParsePropertySection(XElement shapeElement)
        {
            var sections = shapeElement.Children("Section").Where(item => item.GetAttributeValue("N") == "Property");

            return this.GetSectionMap(sections.Select(item => GetSection(item)));
        }

        public Dictionary<string, string> ParseUserSection(XElement shapeElement)
        {
            var sections = shapeElement.Children("Section").Where(item => item.GetAttributeValue("N") == "User");

            return GetSectionMap(sections.Select(item => GetSection(item)));
        }

        private Dictionary<string, string> GetSectionMap(IEnumerable<Section> sections)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();

            foreach (var section in sections)
            {
                foreach (var row in section.Rows)
                {
                    var value = GetCellValue(row, "Value");

                    if (!string.IsNullOrEmpty(value))
                    {
                        var key = row.Name ?? row.Index; map[key] = value;
                    }
                }
            }

            return map;
        }

        private List<UserDef> ParseUserDefs(XElement shapeElement)
        {
            var defs = new List<UserDef>();

            var sections = shapeElement.Children("Section").Where(item => item.GetAttributeValue("N") == "User");

            foreach (var section in sections.Select(item => GetSection(item)))
            {
                var rows = section.Rows;

                foreach (var row in rows)
                {
                    var nameU = row.Name ?? row.Index;

                    if (string.IsNullOrEmpty(nameU))
                        continue;

                    defs.Add(new UserDef()
                    {
                        NameU = nameU,
                        Prompt = GetCellValue(row, "Prompt"),
                        Value = this.ValueForVisioMetadata(row)
                    });
                }
            }

            return defs;
        }

        private ForeignData ParseForeignData(XElement shapeElement)
        {
            var foreignDataEls = shapeElement.Children("ForeignData");

            if (foreignDataEls.Count() == 0)
                return null;

            var foreignData = foreignDataEls[0];
            var relElement = foreignData.Child("Rel");

            string relId = null;

            if (relElement != null)
            {
                relId = relElement.GetAttributeValue("id");
            }

            return new ForeignData()
            {
                ForeignType = foreignData.GetAttributeValue("ForeignType"),
                CompressionType = foreignData.GetAttributeValue("CompressionType"),
                RelId = relId
            };
        }

        private List<CustomPropertyInfo> ParseCustomProperties(XElement shapeEl)
        {
            var props = new List<CustomPropertyInfo>();

            var sections = shapeEl.Children("Section").Where(item => item.GetAttributeValue("N") == "Property");

            foreach (var sec in sections.Select(item => GetSection(item)))
            {
                var rows = sec.Rows;

                foreach (var row in rows)
                {
                    var nameU = row.Name ?? row.Index;

                    if (string.IsNullOrEmpty(nameU))
                        continue;

                    props.Add(new CustomPropertyInfo()
                    {
                        NameU = nameU,
                        Label = GetCellValue(row, "Label"),
                        Prompt = GetCellValue(row, "Prompt"),
                        Type = GetCellValue(row, "Type"),
                        Format = GetCellValue(row, "Format"),
                        Invisible = GetCellValue(row, "Invisible"),
                        LangID = GetCellValue(row, "LangID"),
                        Value = this.ValueForVisioMetadata(row)
                    });
                }
            }

            return props;
        }

        private string ValueForVisioMetadata(Row row, string name = "Value")
        {
            var v = GetCellValue(row.Cells, name);

            if (string.IsNullOrEmpty(v))
                return null;

            var u = GetCellUnit(row.Cells, name);

            if (u == "STR")
                return $"VT4({v})";

            if (!string.IsNullOrEmpty(u))
                return $"VT0({v}):{u}";

            if (Regex.IsMatch(v, @"^-?(?:\d+|\d*\.\d+)(?:e[+-]?\d+)?$", RegexOptions.IgnoreCase))
                return $"VT0({v}):26";

            return $"VT4({v})";
        }

        private Dictionary<string, string> MergeMap(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            var dict = ObjectHelper.CloneObject<Dictionary<string, string>>(target);

            foreach (var kp in source)
            {
                dict[kp.Key] = kp.Value;
            }

            return dict;
        }

        private CustomPropertyInfo[] MergeMetadataRows(List<CustomPropertyInfo> masterRows, List<CustomPropertyInfo> shapeRows)
        {
            var merged = new Dictionary<string, CustomPropertyInfo>();

            if (masterRows != null)
            {
                foreach (var row in masterRows)
                {
                    merged.Add(row.NameU, row);
                }
            }

            foreach (var row in shapeRows)
            {
                var cloneRow = merged.ContainsKey(row.NameU) ? ObjectHelper.CloneObject<CustomPropertyInfo>(row) : new CustomPropertyInfo();

                ObjectHelper.CopyProperties(row, cloneRow, true);

                merged[row.NameU] = cloneRow;
            }

            return merged.Values.ToArray();
        }

        private ImageDataInfo ResolveImageData(ForeignData foreignData, List<PackageRelationship> pageRelationships, List<MediaInfo> media)
        {
            if (string.IsNullOrEmpty(foreignData?.RelId) || pageRelationships == null || media == null)
                return null;

            var target = pageRelationships.FirstOrDefault(item=>item.Id == foreignData.RelId)?.TargetUri?.OriginalString;
            if (string.IsNullOrEmpty(target))
                return null;

            var filename = target.Split('/').LastOrDefault();
            if (string.IsNullOrEmpty(filename))
                return null;

            var mediaEntry = media.FirstOrDefault(item => item.FileName == filename);
            if (mediaEntry == null)
                return null;

            return new ImageDataInfo()
            {
                Href = mediaEntry.DataUri,
                FileName = filename,
                ForeignType = foreignData.ForeignType
            };
        }
    }
}
