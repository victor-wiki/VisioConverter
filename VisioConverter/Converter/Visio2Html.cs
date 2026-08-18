using HtmlAgilityPack;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisioConverter.Extension;
using VisioConverter.Helper;
using VisioConverter.Model;

namespace VisioConverter.Converter
{
    public delegate void PageBeginConvert(int slideIndex);
    public delegate void PageEndConvert(int slideIndex, HtmlConvertInfo htmlInfo);
    public delegate void PageConvertError(int slideIndex, string message);

    public partial class Visio2Html
    {
        private static readonly HtmlDocument document = new HtmlDocument();
        private string filePath;
        private Stream stream;
        private ConvertOption option;
        private bool enableLog = false;

        public event PageBeginConvert OnPageBeginConvert;
        public event PageEndConvert OnPageEndConvert;
        public event PageConvertError OnPageConvertError;

        public Visio2Html(string filePath, ConvertOption option = null)
        {
            this.filePath = filePath;
            this.option = option;
        }

        public Visio2Html(Stream stream, ConvertOption option = null)
        {
            this.stream = stream;
            this.option = option;
        }

        public ConvertResult Convert()
        {
            if (string.IsNullOrEmpty(this.filePath) && this.stream == null)
            {
                throw new ArgumentNullException("Please provide either a file path or a stream!");
            }

            this.enableLog = this.option?.EnableLog ?? false;
            LogHelper.DefaultLogFolder = this.option?.DefaultLogFolder;

            ConvertResult result = new ConvertResult() { Infos = new List<HtmlConvertInfo>() };

            VsdxParser parser = !string.IsNullOrEmpty(this.filePath) ? new VsdxParser(this.filePath) : new VsdxParser(this.stream);

            VsdxInfo vsdxInfo = parser.Parse();

            int pageIndex = 0;

            foreach (var page in vsdxInfo.Pages)
            {
                HtmlConvertInfo info = new HtmlConvertInfo() { Width = InchToPixel(page.Width), Height = InchToPixel(page.Height), Number = pageIndex + 1 };

                try
                {
                    if (this.option != null && this.option.PageNumbers != null && this.option.PageNumbers.Count > 0)
                    {
                        if (!this.option.PageNumbers.Contains(pageIndex + 1))
                        {
                            pageIndex++;
                            continue;
                        }
                    }

                    this.Log($"Start to convert page {pageIndex + 1}...");

                    if (this.OnPageBeginConvert != null)
                    {
                        this.OnPageBeginConvert(pageIndex);
                    }

                    HtmlDocument doc = new HtmlDocument();

                    var svg = this.RenderPage(page);

                    doc.DocumentNode.AppendChild(svg);

                    StringBuilder sbHtml = new StringBuilder();
                    TextWriter tw = new StringWriter(sbHtml, CultureInfo.InvariantCulture);

                    doc.Save(tw);

                    info.Html = sbHtml.ToString();

                    info.IsOK = true;

                    result.Infos.Add(info);

                    if (this.OnPageEndConvert != null)
                    {
                        this.OnPageEndConvert(pageIndex, info);
                    }

                    this.Log($"End to convert page {pageIndex + 1}.");

                    this.Log(Environment.NewLine);

                }
                catch (Exception ex)
                {
                    info.IsOK = false;
                    info.Message = ex.Message;

                    this.Log(ExceptionHelper.GetExceptionDetails(ex), LogType.Error);

                    result.Infos.Add(info);

                    if (this.OnPageConvertError != null)
                    {
                        this.OnPageConvertError(pageIndex, info.Message);
                    }
                }

                pageIndex++;
            }

            return result;
        }

        private HtmlNode RenderPage(Page page)
        {
            var svg = document.CreateSvg();

            var w = InchToPixel(page.Width);
            var h = InchToPixel(page.Height);

            svg.SetAttributeValue("viewBox", $"0 0 {w} {h}");
            svg.SetAttributeValue("width", "100%");
            svg.SetAttributeValue("height", "100%");
            svg.SetAttributeValue("fill-rule", "evenodd");
            svg.SetAttributeValue("clip-rule", "evenodd");

            string svgStyle = $"maxWidth:{w}px;background:white";

            svg.SetAttributeValue("style", svgStyle);

            var style = document.CreateElement("style");

            style.SetAttributeValue("type", "text/css");
            style.InnerHtml = ".vsdx-hidden{visibility:hidden}";

            svg.AppendChild(style);

            var defs = document.CreateElement("defs");

            svg.AppendChild(defs);

            var arrowCounter = new ArrowCounterInfo() { Value = 0 };

            var strokeScale = page.DrawingScale ?? (page.DrawingUnitInInches.HasValue ? (1.0f / page.DrawingUnitInInches) : 1.0f);
            var fontScale = strokeScale;
            var themeColors = page.ThemeColors;

            foreach (var shape in page.Shapes)
            {
                svg.AppendChild(this.RenderShape(shape, page.Height, defs, arrowCounter, strokeScale, fontScale, themeColors, page));
            }

            return svg;
        }

        private HtmlNode RenderShape(Shape shape, float? pageHeight, HtmlNode defs, ArrowCounterInfo arrowCounter, float? strokeScale, float? fontScale, Dictionary<string, string> themeColors = null, Page page = null)
        {
            if (fontScale == null)
                fontScale = strokeScale;

            var g = document.CreateElement("g");

            if (!string.IsNullOrEmpty(shape.Id))
            {
                var safeId = Regex.Replace(shape.Id, @"[^A-Za-z0-9_-]", "_");
                var idPrefix = shape.Type == "Group" ? "group" : "shape";

                g.SetAttributeValue("id", $"{idPrefix}{safeId}");
                g.SetAttributeValue("data-shape-id", shape.Id);
            }

            SetVisioAttribute(g, "mID", shape.Id);
            SetVisioAttribute(g, "groupContext", shape.Type == "Group" ? "group" : "shape");

            if (shape.HasLayerMember)
            {
                string layerValue = string.Join(",", shape.LayerMembers);

                g.SetAttributeValue("data-layers", GetSafeXml(layerValue));
                SetVisioAttribute(g, "layerMember", layerValue);
            }

            var layerInfo = this.GetShapeLayerInfo(shape, page);

            if (layerInfo.Hidden)
                g.SetAttributeValue("display", "none");

            this.appendShapeMetadata(g, shape);

            // Dedicated 1D connector rendering uses page-coordinate geometry instead of
            // shape-local transforms. This avoids collapsing routed connectors and keeps
            // BeginX/EndX fallbacks consistent with Visio.
            if (shape.Is1D && !shape.HasSubShape)
            {
                var pathData = this.BuildConnectorPath(shape, pageHeight);

                if (!string.IsNullOrEmpty(pathData))
                {
                    var path = document.CreateElement("path");

                    path.SetAttributeValue("d", pathData);
                    path.SetAttributeValue("fill", "none");

                    var strokeColor = shape.LinePattern == 0 ? "none" : this.GetShapeStrokeColor(shape, themeColors, page);
                    var effectiveWeight = Math.Max(InchToPixel(shape.LineWeight ?? 0.01f) * (strokeScale ?? 1), 1.5f);

                    path.SetAttributeValue("stroke", strokeColor);
                    path.SetAttributeValue("stroke-width", effectiveWeight.ToString());

                    var dashArray = GetDashArray(shape.LinePattern ?? 1f, effectiveWeight);
                    if (!string.IsNullOrEmpty(dashArray))
                        path.SetAttributeValue("stroke-dasharray", dashArray);

                    path.SetAttributeValue("stroke-linejoin", "round");

                    if (shape.BeginArrow.HasValue && shape.BeginArrow > 0)
                    {
                        var markerId = $"arrow-begin-{arrowCounter.Value++}";

                        defs.AppendChild(this.CreateArrowMarker(markerId, strokeColor, true));

                        path.SetAttributeValue("marker-start", $"url(#{markerId})");
                    }

                    if (shape.EndArrow.HasValue && shape.EndArrow > 0)
                    {
                        var markerId = $"arrow-end-{arrowCounter.Value++}";

                        defs.AppendChild(this.CreateArrowMarker(markerId, strokeColor, false));

                        path.SetAttributeValue("marker-end", $"url(#{markerId})");
                    }

                    g.AppendChild(path);
                }

                this.AppendTextNode(g, shape, pageHeight, fontScale, true);

                return g;
            }

            var px = InchToPixel(shape.PinX ?? 0);
            var py = InchToPixel(pageHeight - (shape.PinY ?? 0));
            var lpx = InchToPixel(shape.LocPinX ?? 0);
            var lpy = InchToPixel((shape.Height ?? 0) - (shape.LocPinY ?? 0));
            var angleDeg = -(shape.Angle ?? 0) * (180 / Math.PI);

            var transform = $"translate({px - lpx}, {py - lpy})";

            if (Math.Abs(angleDeg) > 0.01f)
            {
                transform += $" rotate({angleDeg}, {lpx}, {lpy})";
            }

            if (shape.FlipX || shape.FlipY)
            {
                var sx = shape.FlipX ? -1 : 1;
                var sy = shape.FlipY ? -1 : 1;

                transform += $" translate({(shape.FlipX ? InchToPixel(shape.Width ?? 0) : 0)}, {(shape.FlipY ? InchToPixel(shape.Height ?? 0) : 0)}) scale({sx}, {sy})";
            }

            g.SetAttributeValue("transform", transform);

            if (shape.HasGeometry)
            {
                Action<string, Section, PathAppendOption> appendPath = (pathData, geo, options) =>
                {
                    if (string.IsNullOrEmpty(pathData))
                        return;

                    var paintFill = options?.PaintFill != false;
                    var paintStroke = options?.PaintStroke != false;
                    var path = document.CreateElement("path");

                    path.SetAttributeValue("d", pathData);

                    var fillColor = this.GetFillPaint(shape, defs, themeColors, layerInfo);

                    if (paintFill != true || geo.NoFill == true || string.IsNullOrEmpty(fillColor) || shape.FillPattern == 0)
                    {
                        path.SetAttributeValue("fill", "none");
                    }
                    else
                    {
                        path.SetAttributeValue("fill", fillColor);

                        var fillOpacity = this.GetFillOpacity(shape, fillColor, layerInfo);

                        if (fillOpacity != null)
                            path.SetAttributeValue("fill-opacity", fillOpacity.ToString());

                        if (HasMultipleSubpaths(pathData))
                        {
                            path.SetAttributeValue("fill-rule", "evenodd");
                            path.SetAttributeValue("clip-rule", "evenodd");
                        }
                    }

                    // Stroke. lineWeight is always stored in inches, but the coordinate
                    // space we emit is in the drawing"s native unit (mm/inches/...) scaled
                    // up by 96. `strokeScale` converts inch-valued line weights into that
                    // coordinate space so strokes stay visually proportional to the drawing.
                    if (paintStroke != true || geo.NoLine == true || shape.LinePattern == 0)
                    {
                        path.SetAttributeValue("stroke", "none");
                    }
                    else
                    {
                        path.SetAttributeValue("stroke", this.GetShapeStrokeColor(shape, themeColors, page));

                        var effectiveWeight = InchToPixel(shape.LineWeight ?? 0) * (strokeScale ?? 1);
                        path.SetAttributeValue("stroke-width", Math.Max(effectiveWeight, 0.5f).ToString());
                        var dashArray = GetDashArray(shape.LinePattern ?? 0, effectiveWeight);

                        if (!string.IsNullOrEmpty(dashArray))
                        {
                            path.SetAttributeValue("stroke-dasharray", dashArray);
                        }
                    }

                    path.SetAttributeValue("stroke-linejoin", "round");

                    if (geo.NoShow == true)
                        AddClass(path, "vsdx-hidden");

                    if (paintStroke && shape.BeginArrow.HasValue && shape.BeginArrow > 0)
                    {
                        var markerId = $"arrow-begin-{arrowCounter.Value++}";
                        var marker = this.CreateArrowMarker(markerId, this.GetShapeStrokeColor(shape, themeColors, page), true);

                        defs.AppendChild(marker);

                        path.SetAttributeValue("marker-start", $"url(#{markerId})");
                    }
                    if (paintStroke && shape.EndArrow.HasValue && shape.EndArrow > 0)
                    {
                        var markerId = $"arrow-end-{arrowCounter.Value++}";
                        var marker = this.CreateArrowMarker(markerId, this.GetShapeStrokeColor(shape, themeColors, page), false);

                        defs.AppendChild(marker);

                        path.SetAttributeValue("marker-end", $"url(#{markerId})");
                    }

                    g.AppendChild(path);
                };

                CompoundRunInfo compoundRun = null;
                List<StrokeInfo> strokeQueue = new List<StrokeInfo>();

                Action flushCompoundRun = () =>
                {
                    if (compoundRun == null)
                        return;

                    appendPath(string.Join(" ", compoundRun.Paths), new Section()
                    {
                        NoFill = false,
                        NoLine = compoundRun.NoLine,
                        NoShow = compoundRun.NoShow
                    }, null);

                    compoundRun = null;
                };

                Action flushStrokeQueue = () =>
                {
                    foreach (var item in strokeQueue)
                    {
                        appendPath(item.PathData, item.Geometry, new PathAppendOption { PaintFill = false });
                    }

                    strokeQueue.Clear();
                };

                var hasVisibleGeometry = shape.Geometries.Any(geo => geo.NoShow != true);
                var geometryToRender = hasVisibleGeometry
                  ? shape.Geometries.Where(geo => geo.NoShow != true)
                  : shape.Geometries;

                foreach (var geo in geometryToRender)
                {
                    var strokePathData = GeometryToPath(shape, geo.Rows, shape.Width, shape.Height, new GeometryToPathResolveOption()
                    {
                        ConnectInternalMoves = false
                    });

                    var noEffectiveLine = geo.NoLine ?? shape.LinePattern == 0;
                    var hasPaintedFill = geo.NoFill != true && shape.FillPattern != 0 && !string.IsNullOrEmpty(this.GetFillPaint(shape, defs, themeColors, layerInfo));
                    var fillPathData = hasPaintedFill
                      ? GeometryToPath(shape, geo.Rows, shape.Width, shape.Height, new GeometryToPathResolveOption() { ConnectInternalMoves = true })
                      : strokePathData;

                    if (string.IsNullOrEmpty(strokePathData) && string.IsNullOrEmpty(fillPathData))
                        continue;

                    var canCompound =
                      shape.BeginArrow.IsEmpty() &&
                      shape.EndArrow.IsEmpty() &&
                      noEffectiveLine &&
                      geo.NoFill != true &&
                      shape.FillPattern != 0 &&
                      hasPaintedFill;

                    if (canCompound)
                    {
                        if (compoundRun == null || compoundRun.NoLine != geo.NoLine || compoundRun.NoShow != geo.NoShow)
                        {
                            flushCompoundRun();

                            compoundRun = new CompoundRunInfo() { NoLine = geo.NoLine, NoShow = geo.NoShow, Paths = new List<string>() };
                        }

                        compoundRun.Paths.Add(strokePathData);
                    }
                    else
                    {
                        flushCompoundRun();

                        if (hasPaintedFill && !noEffectiveLine && shape.BeginArrow.IsEmpty() && shape.EndArrow.IsEmpty())
                        {
                            appendPath(fillPathData, geo, new PathAppendOption { PaintStroke = false });

                            if (!string.IsNullOrEmpty(strokePathData))
                                strokeQueue.Add(new StrokeInfo() { PathData = strokePathData, Geometry = geo });
                        }
                        else if (!noEffectiveLine)
                        {
                            if (!string.IsNullOrEmpty(strokePathData))
                                strokeQueue.Add(new StrokeInfo() { PathData = strokePathData, Geometry = geo });
                        }
                        else
                        {
                            appendPath(hasPaintedFill ? fillPathData : strokePathData, geo, null);
                        }
                    }
                }

                flushCompoundRun();
                flushStrokeQueue();
            }
            else if (!shape.HasGeometry && !shape.HasSubShape && shape.Width > 0 && shape.Height > 0)
            {
                // No geometry and no sub-shapes - draw a rectangle as fallback
                var rect = document.CreateElement("rect");

                rect.SetAttributeValue("x", "0");
                rect.SetAttributeValue("y", "0");
                rect.SetAttributeValue("width", InchToPixel(shape.Width).ToString());
                rect.SetAttributeValue("height", InchToPixel(shape.Height).ToString());

                var rectFill = this.GetFillPaint(shape, defs, themeColors, layerInfo);

                if (!string.IsNullOrEmpty(rectFill) && shape.FillPattern != 0)
                {
                    rect.SetAttributeValue("fill", rectFill);

                    float? fillOpacity = this.GetFillOpacity(shape, rectFill, layerInfo);

                    if (fillOpacity != null)
                        rect.SetAttributeValue("fill-opacity", fillOpacity.Value.ToString());
                }
                else
                {
                    rect.SetAttributeValue("fill", "none");
                }

                rect.SetAttributeValue("stroke", shape.LinePattern == 0 ? "none" : this.GetShapeStrokeColor(shape, themeColors, page));
                rect.SetAttributeValue("stroke-width", Math.Max(InchToPixel(shape.LineWeight) * (strokeScale ?? 1), 0.5f).ToString());

                if (shape.Rounding > 0)
                {
                    rect.SetAttributeValue("rx", InchToPixel(shape.Rounding).ToString());
                    rect.SetAttributeValue("ry", InchToPixel(shape.Rounding).ToString());
                }

                g.AppendChild(rect);
            }

            foreach (var sub in shape.SubShapes)
            {
                g.AppendChild(this.RenderShape(sub, shape.Height, defs, arrowCounter, strokeScale, fontScale, themeColors, page));
            }

            this.AppendImageNode(g, shape);
            this.AppendTextNode(g, shape, pageHeight, fontScale, false);

            return g;
        }

        private ShapeLayerInfo GetShapeLayerInfo(Shape shape, Page pageContext)
        {
            if (pageContext.Layers == null || shape.HasLayerMember == false)
                return new ShapeLayerInfo();

            var matchedLayers = pageContext.Layers
              .Where(item => shape.LayerMembers.Contains(item.Index));

            if (matchedLayers.Count() == 0)
                return new ShapeLayerInfo();

            var visibleLayers = matchedLayers.Where((layer) => layer.Visible != false).ToArray();

            if (visibleLayers.Length == 0)
                return new ShapeLayerInfo() { Hidden = true };

            var themedAccentStroke = !string.IsNullOrEmpty(shape.StyleInfo?.LineColorFormula) ? Regex.IsMatch(shape.StyleInfo.LineColorFormula, @"AccentColor|LineColor", RegexOptions.IgnoreCase) : false;
            var themedAccentFill = !string.IsNullOrEmpty(shape.StyleInfo?.FillForegroundFormula) ? Regex.IsMatch(shape.StyleInfo.FillForegroundFormula, @"LineColor|FillColor", RegexOptions.IgnoreCase) : false;
            var monochromeColor = visibleLayers.Length == 1
              && (!string.IsNullOrEmpty(visibleLayers[0].Color) ? Regex.IsMatch(visibleLayers[0].Color, @"^#[0-9a-f]{6}$", RegexOptions.IgnoreCase) : false)
              && themedAccentStroke
              && themedAccentFill
              ? visibleLayers[0].Color
              : null;

            return new ShapeLayerInfo() { MonochromeColor = monochromeColor };
        }

        private void appendShapeMetadata(HtmlNode target, Shape shape)
        {
            var titleText = shape.Title ?? shape.Name ?? shape.NameU;

            if (!string.IsNullOrEmpty(titleText))
            {
                var title = document.CreateElement("title");

                title.InnerHtml = GetSafeXml(titleText);

                target.AppendChild(title);
            }

            if (shape.HasCustomProperty)
            {
                var custProps = CreateVisioElement("custProps");

                foreach (var prop in shape.CustomPropertyInfos)
                {
                    var cp = CreateVisioElement("cp");

                    SetVisioAttribute(cp, "nameU", prop.NameU);
                    SetVisioAttribute(cp, "lbl", prop.Label);
                    SetVisioAttribute(cp, "prompt", prop.Prompt);
                    SetVisioAttribute(cp, "type", prop.Type);
                    SetVisioAttribute(cp, "format", prop.Format);
                    SetVisioAttribute(cp, "invis", prop.Invisible == "1" ? "true" : prop.Invisible == "0" ? "false" : prop.Invisible);
                    SetVisioAttribute(cp, "langID", prop.LangID);
                    SetVisioAttribute(cp, "val", prop.Value);

                    custProps.AppendChild(cp);
                }

                target.AppendChild(custProps);
            }

            if (shape.HasUserDef)
            {
                var userDefs = CreateVisioElement("userDefs");

                foreach (var def in shape.UserDefs)
                {
                    var ud = CreateVisioElement("ud");

                    SetVisioAttribute(ud, "nameU", def.NameU);
                    SetVisioAttribute(ud, "prompt", def.Prompt);
                    SetVisioAttribute(ud, "val", def.Value);

                    userDefs.AppendChild(ud);
                }

                target.AppendChild(userDefs);
            }
        }

        private string BuildConnectorPath(Shape shape, float? pageHeight)
        {
            var points = new List<PointInfo>();
            var hasMoveTo = false;

            if (shape.HasGeometry)
            {
                foreach (var geo in shape.Geometries)
                {
                    if (geo.NoShow == true)
                        continue;

                    foreach (var row in geo.Rows)
                    {
                        if (row.X == null || row.Y == null)
                            continue;

                        var localX = row.X;
                        var localY = row.Y;

                        if ((row.Type == "RelMoveTo" || row.Type == "RelLineTo" || row.Type == "RelEllipticalArcTo" || row.Type == "RelQuadBezTo" || row.Type == "RelCubBezTo")
                          && shape.Width != null && shape.Height != null)
                        {
                            localX = row.X.Value * shape.Width.Value;
                            localY = row.Y.Value * shape.Height.Value;
                        }

                        if (row.Type == "MoveTo" || row.Type == "RelMoveTo")
                            hasMoveTo = true;

                        if (!new string[] { "MoveTo", "RelMoveTo", "LineTo", "RelLineTo", "ArcTo", "EllipticalArcTo", "RelEllipticalArcTo", "SplineStart", "SplineKnot", "NURBSTo", "PolylineTo", "RelQuadBezTo", "RelCubBezTo" }.Contains(row.Type))
                        {
                            continue;
                        }

                        points.Add(ToConnectorPoint(shape, pageHeight, localX.Value, localY.Value));
                    }
                }
            }

            var begin = (shape.BeginX != null && shape.BeginY != null)
                          ? new PointInfo() { X = InchToPixel(shape.BeginX.Value), Y = InchToPixel(pageHeight - shape.BeginY.Value) }
                          : default(PointInfo?);
            var end = (shape.EndX != null && shape.EndY != null)
                          ? new PointInfo() { X = InchToPixel(shape.EndX.Value), Y = InchToPixel(pageHeight - shape.EndY.Value) }
                          : default(PointInfo?);

            if (points.Count > 0 && !hasMoveTo && begin.HasValue)
            {
                points.Insert(0, begin.Value);
            }

            if (points.Count == 1 && end.HasValue)
            {
                var only = points[0];

                if (Math.Abs(only.X - end.Value.X) > 0.1 || Math.Abs(only.Y - end.Value.Y) > 0.1f)
                {
                    points.Add(end.Value);
                }
            }

            if (points.Count >= 2)
            {
                var deduped = new List<PointInfo>() { points[0] };

                for (var i = 1; i < points.Count; i++)
                {
                    var prev = deduped[deduped.Count - 1];
                    var next = points[i];

                    if (Math.Abs(prev.X - next.X) > 0.1 || Math.Abs(prev.Y - next.Y) > 0.1)
                        deduped.Add(next);
                }

                if (deduped.Count >= 2)
                {
                    return $"M {deduped[0].X} {deduped[0].Y} " + string.Join(" ", deduped.Skip(1).Select(pt => $"L {pt.X} {pt.Y}"));
                }
            }

            if (begin.HasValue && end.HasValue && (Math.Abs(begin.Value.X - end.Value.X) > 0.1 || Math.Abs(begin.Value.Y - end.Value.Y) > 0.1f))
            {
                return $"M {begin.Value.X} {begin.Value.Y} L {end.Value.X} {end.Value.Y}";
            }

            return null;
        }

        private HtmlNode CreateArrowMarker(string id, string color, bool isStart)
        {
            var marker = document.CreateElement("marker");

            marker.SetAttributeValue("id", id);
            marker.SetAttributeValue("markerWidth", "10");
            marker.SetAttributeValue("markerHeight", "7");
            marker.SetAttributeValue("orient", "auto");

            if (isStart)
            {
                marker.SetAttributeValue("refX", "0");
                marker.SetAttributeValue("refY", "3.5");

                var polygon = document.CreateElement("polygon");

                polygon.SetAttributeValue("points", "10 0, 10 7, 0 3.5");
                polygon.SetAttributeValue("fill", color);

                marker.AppendChild(polygon);
            }
            else
            {
                marker.SetAttributeValue("refX", "10");
                marker.SetAttributeValue("refY", "3.5");

                var polygon = document.CreateElement("polygon");

                polygon.SetAttributeValue("points", "0 0, 10 3.5, 0 7");
                polygon.SetAttributeValue("fill", color);

                marker.AppendChild(polygon);
            }

            return marker;
        }

        private string GetShapeStrokeColor(Shape shape, Dictionary<string, string> themeColors, Page pageContext)
        {
            var info = this.GetShapeLayerInfo(shape, pageContext);

            return info.MonochromeColor ?? shape.LineColor ?? themeColors.GetValue("dk1") ?? "#000000";
        }

        private void AppendTextNode(HtmlNode target, Shape shape, float? pageHeight, float? fontScale, bool isConnector = false)
        {
            if (string.IsNullOrEmpty(shape.Text))
                return;

            var text = document.CreateElement("text");
            var fontSize = shape.FontSize.HasValue ? InchToPixel(shape.FontSize.Value) * fontScale : 12f;
            var fill = shape.FontColor ?? "#000000";

            text.SetAttributeValue("text-anchor", "middle");
            text.SetAttributeValue("dominant-baseline", "central");
            text.SetAttributeValue("font-size", fontSize.ToString());
            text.SetAttributeValue("fill", fill);
            text.SetAttributeValue("font-family", FormatFontFamily(shape.FontFamily));

            if (shape.Bold)
                text.SetAttributeValue("font-weight", "bold");
            if (shape.Italic)
                text.SetAttributeValue("font-style", "italic");

            var maxWidthPx = InchToPixel(Math.Abs(shape.TextWidth ?? shape.Width ?? 0));
            var lines = WrapTextLines(GetSafeXml(shape.Text), maxWidthPx, fontSize);

            if (isConnector)
            {
                var pt = ToConnectorPoint(shape, pageHeight, shape.TextPinX ?? shape.LocPinX ?? 0, shape.TextPinY ?? shape.LocPinY ?? 0);
                text.SetAttributeValue("x", pt.X.ToString());
                text.SetAttributeValue("y", pt.Y.ToString());
            }
            else
            {
                text.SetAttributeValue("x", InchToPixel(shape.TextPinX ?? (shape.Width.Value / 2.0f)).ToString());
                text.SetAttributeValue("y", InchToPixel((shape.Height ?? 0) - (shape.TextPinY ?? (shape.Height.Value / 2.0f))).ToString());
            }

            if (shape.HasTextRun && shape.TextRuns.Count > 1 && lines.Length <= 1)
            {
                text.InnerHtml = "";

                var richRuns = shape.TextRuns.Where(run => !string.IsNullOrEmpty(run.Text));

                foreach (var run in richRuns)
                {
                    var tspan = document.CreateElement("tspan");
                    var runFontSize = run.Font != null && run.Font.Size != 0 ? InchToPixel(run.Font.Size) * fontScale : fontSize;

                    tspan.SetAttributeValue("font-family", FormatFontFamily(run.Font?.Family ?? shape.FontFamily));
                    tspan.SetAttributeValue("font-size", runFontSize.ToString());
                    tspan.SetAttributeValue("fill", run.Font?.Color ?? fill);
                    tspan.SetAttributeValue("font-weight", run?.Font?.Bold == true ? "bold" : "normal");
                    tspan.SetAttributeValue("font-style", run?.Font?.Italic == true ? "italic" : "normal");

                    if (run?.Font?.Underline == true)
                        tspan.SetAttributeValue("text-decoration", "underline");

                    tspan.InnerHtml = GetSafeXml(run.Text);

                    text.AppendChild(tspan);
                }
            }
            else if (lines.Length <= 1)
            {
                text.InnerHtml = lines.Length > 0 ? (lines[0] ?? "") : "";
            }
            else
            {
                var lineHeight = fontSize * 1.2;
                var centerY = isConnector
                  ? (ToConnectorPoint(shape, pageHeight, shape.TextPinX ?? shape.LocPinX ?? 0, shape.TextPinY ?? shape.LocPinY ?? 0).Y)
                  : InchToPixel((shape.Height ?? 0) - (shape.TextPinY ?? (shape.Height.Value / 2.0f)));
                var startY = centerY - ((lines.Length - 1) * lineHeight / 2.0f);
                var x = isConnector
                  ? ToConnectorPoint(shape, pageHeight, shape.TextPinX ?? shape.LocPinX ?? 0, shape.TextPinY ?? shape.LocPinY ?? 0).X
                  : InchToPixel(shape.TextPinX ?? (shape.Width.Value / 2));

                text.InnerHtml = "";

                for (var i = 0; i < lines.Length; i++)
                {
                    var tspan = document.CreateElement("tspan");

                    tspan.SetAttributeValue("x", x.ToString());
                    tspan.SetAttributeValue("y", (startY + i * lineHeight).ToString());
                    tspan.InnerHtml = lines[i];

                    text.AppendChild(tspan);
                }
            }

            target.AppendChild(text);
        }

        private string GetFillPaint(Shape shape, HtmlNode defs, Dictionary<string, string> themeColors, ShapeLayerInfo layerInfo = null)
        {
            if (!string.IsNullOrEmpty(layerInfo?.MonochromeColor))
                return "#FFFFFF";

            var fillColor = GetFallbackFill(shape, themeColors);

            if (shape.FillPattern >= 25 && shape.FillPattern <= 40 && !string.IsNullOrEmpty(shape.FillBackground) && !string.IsNullOrEmpty(fillColor))
            {
                if (shape.FillBackground.ToUpper() == fillColor.ToUpper())
                    return fillColor;

                var gradientId = $"grad_{Regex.Replace((shape.Id ?? "shape"), @"[^A-Za-z0-9_-]", "_")}_${(int)Math.Round(shape.FillPattern)}";

                var gradientNode = defs.ChildNodes.FirstOrDefault(item => item.GetAttributeValue("id", "") == gradientId);

                if (gradientNode == null)
                {
                    defs.AppendChild(this.CreateGradientDef(gradientId, shape));
                }

                return $"url(#{gradientId})";
            }

            return fillColor;
        }

        private string GetFallbackFill(Shape shape, Dictionary<string, string> themeColors)
        {
            return shape.FillForeground ?? ((string.IsNullOrEmpty(shape.FillForeground) && !string.IsNullOrEmpty(shape.FillBackground) && !string.IsNullOrEmpty(shape.FontColor) && ColorHelper.IsLightColor(shape.FontColor))
              ? shape.FillBackground
              : ((string.IsNullOrEmpty(shape.FillForeground) && themeColors.HasValue("lt1") && !string.IsNullOrEmpty(shape.FontColor) && !ColorHelper.IsLightColor(shape.FontColor)) ? themeColors.GetValue("lt1") : shape.FillBackground));
        }

        private float? GetFillOpacity(Shape shape, string fillPaint, ShapeLayerInfo layerInfo = null)
        {
            if (string.IsNullOrEmpty(fillPaint) || fillPaint == "none")
                return null;

            if (!string.IsNullOrEmpty(layerInfo?.MonochromeColor))
                return 1;

            if (fillPaint.StartsWith("url(#"))
                return null;

            var opacity = ClampOpacityFromTransparency(shape.FillForegroundTrans, 0);

            return opacity < 1 ? opacity : null;
        }

        private void AppendImageNode(HtmlNode target, Shape shape)
        {
            if (string.IsNullOrEmpty(shape?.Image?.Href))
                return;

            var image = document.CreateElement("image");

            image.SetAttributeValue("x", (InchToPixel(shape.Image.X ?? 0)).ToString());
            image.SetAttributeValue("y", (InchToPixel(shape.Image.Y ?? 0).ToString()));
            image.SetAttributeValue("width", (InchToPixel(shape.Image.Width ?? shape.Width ?? 0)).ToString());
            image.SetAttributeValue("height", (InchToPixel(shape.Image.Height ?? shape.Height ?? 0)).ToString());
            image.SetAttributeValue("xlink:href", shape.Image.Href);
            image.SetAttributeValue("preserveAspectRatio", "xMidYMid meet");

            target.AppendChild(image);
        }

        private string[] WrapTextLines(string text, float? maxWidthPx, float? fontSize)
        {
            var explicitLines = text.Split("\n").Select(line => line.Trim()).Where(item => !string.IsNullOrEmpty(item)).ToArray();

            if (explicitLines.Length > 1)
                return explicitLines;

            var source = explicitLines.Length == 1 ? explicitLines[0] : text.Trim();

            if (string.IsNullOrEmpty(source))
                return [];

            if (maxWidthPx == null || maxWidthPx <= 0 || fontSize == null)
                return [source];

            var words = source.Split(" ").Where(item => !string.IsNullOrEmpty(item.Trim())).ToArray();

            if (words.Length <= 1)
                return [source];

            var avgCharWidth = fontSize * 0.36f;
            var maxChars = Math.Max(8, Math.Floor(maxWidthPx.Value / avgCharWidth.Value));

            if (source.Length <= maxChars)
                return [source];

            var lines = new List<string>();
            var current = words[0];

            for (var i = 1; i < words.Length; i++)
            {
                var candidate = $"{current} {words[i]}";

                if (candidate.Length <= maxChars)
                {
                    current = candidate;
                }
                else
                {
                    lines.Add(current);

                    current = words[i];
                }
            }

            if (!string.IsNullOrEmpty(current))
                lines.Add(current);

            if (lines.Count > 2 && words.Length >= 4)
            {
                var bestSplit = 1;
                var bestScore = int.MaxValue;

                for (var i = 1; i < words.Length; i++)
                {
                    var left = string.Join(" ", words.Take(i));
                    var right = string.Join(" ", words.Skip(i));
                    var score = Math.Abs(left.Length - right.Length);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestSplit = i;
                    }
                }

                return [string.Join(" ", words.Take(bestSplit)), string.Join(" ", words.Skip(bestSplit))];
            }

            return lines.ToArray();
        }

        private void AppendImageNode(HtmlNode target, Shape shape, string svgNS)
        {
            if (string.IsNullOrEmpty(shape?.Image?.Href))
                return;

            var image = document.CreateElement("image");

            image.SetAttributeValue("x", (InchToPixel(shape.Image.X ?? 0)).ToString());
            image.SetAttributeValue("y", (InchToPixel(shape.Image.Y ?? 0).ToString()));
            image.SetAttributeValue("width", (InchToPixel(shape.Image.Width ?? shape.Width ?? 0)).ToString());
            image.SetAttributeValue("height", (InchToPixel(shape.Image.Height ?? shape.Height ?? 0)).ToString());
            image.SetAttributeValue("xlink:href", shape.Image.Href);
            image.SetAttributeValue("preserveAspectRatio", "xMidYMid meet");

            target.AppendChild(image);
        }

        private HtmlNode CreateGradientDef(string id, Shape shape)
        {
            var isRadial = IsRadialGradientPattern(shape.FillPattern);
            var gradient = document.CreateElement(isRadial ? "radialGradient" : "linearGradient");

            gradient.SetAttributeValue("id", id);

            if (isRadial)
            {
                gradient.SetAttributeValue("cx", "50%");
                gradient.SetAttributeValue("cy", "50%");
                gradient.SetAttributeValue("r", "50%");
            }
            else
            {
                var rad = GetGradientAngle(shape) * Math.PI / 180;

                var x1 = 50 - 50 * Math.Cos(rad);
                var y1 = 50 + 50 * Math.Sin(rad);
                var x2 = 50 + 50 * Math.Cos(rad);
                var y2 = 50 - 50 * Math.Sin(rad);

                gradient.SetAttributeValue("x1", $"{x1.ToFixed(1)}%");
                gradient.SetAttributeValue("y1", $"{y1.ToFixed(1)}%");
                gradient.SetAttributeValue("x2", $"{x2.ToFixed(1)}%");
                gradient.SetAttributeValue("y2", $"{y2.ToFixed(1)}%");
            }

            var stops = shape.HasFillGradientStop ? shape.FillGradientStops
              : new List<GradientStop>() {
                   new GradientStop() {
                        Offset = 0,
                        Color = shape.FillBackground ?? "#FFFFFF",
                        Opacity = ClampOpacityFromTransparency(shape.FillBackgroundTrans, shape.FillForegroundTrans ?? 0)
                      },
                   new GradientStop() {
                    Offset = 100,
                    Color = shape.FillForeground ?? shape.FillBackground ?? "#CCCCCC",
                    Opacity = ClampOpacityFromTransparency(shape.FillForegroundTrans, 0)
                }
              };

            this.AppendGradientStops(gradient, stops);

            return gradient;
        }

        private float GetGradientAngle(Shape shape)
        {
            if (shape.FillGradientDir.HasValue)
                return shape.FillGradientDir.Value * 45;

            var patternAngles = new Dictionary<int, int>()
            {
                {25, 0},
                {26, 90},
                {27, 45},
                {28, 315},
                {29, 0},
                {30, 90},
                {33, 0},
                {34, 90},
                {35, 45},
                {36, 315},
                {40, 0 }
           };

            var fp = (int)Math.Round(shape.FillPattern);

            return patternAngles.ContainsKey(fp) ? patternAngles[fp] : 0;
        }

        private void AppendGradientStops(HtmlNode gradient, List<GradientStop> stops)
        {
            foreach (var stopData in stops)
            {
                var stop = document.CreateElement("stop");
                stop.SetAttributeValue("offset", $"{stopData.Offset}%");
                stop.SetAttributeValue("stop-color", stopData.Color);

                if (stopData.Opacity < 1)
                    stop.SetAttributeValue("stop-opacity", stopData.Opacity.ToString());

                gradient.AppendChild(stop);
            }
        }
    }
}
