using HtmlAgilityPack;
using System.Text;
using System.Text.RegularExpressions;
using VisioConverter.Helper;
using VisioConverter.Model;

namespace VisioConverter.Converter
{
    public partial class Visio2Html
    {
        public const float DPI = 96.0f;

        private static float InchToPixel(float? inches)
        {
            return (inches ?? 0) * DPI;
        }

        private static string GetSafeXml(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            return Regex.Replace(s, @"[\x00-\x08\x0B\x0C\x0E-\x1F\uFFFE\uFFFF]", "");
        }

        private static void SetVisioAttribute(HtmlNode el, string name, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            el.SetAttributeValue($"v:{name}", GetSafeXml(value));
        }

        private static HtmlNode CreateVisioElement(string localName)
        {
            return document.CreateElement(localName);
        }

        private static PointInfo ToConnectorPoint(Shape shape, float? pageHeight, float x, float y)
        {
            var pinX = shape.PinX ?? 0;
            var pinY = shape.PinY ?? 0;
            var locPinX = shape.LocPinX ?? (shape.Width / 2.0);
            var locPinY = shape.LocPinY ?? (shape.Height / 2.0);
            var dx = x - locPinX;
            var dy = y - locPinY;
            var angle = shape.Angle ?? 0;
            var cosA = Math.Cos(angle);
            var sinA = Math.Sin(angle);
            var px = pinX + dx * cosA - dy * sinA;
            var py = pinY + dx * sinA + dy * cosA;

            return new PointInfo()
            {
                X = InchToPixel((float)px),
                Y = InchToPixel((float)(pageHeight - py))
            };
        }

        private static string GetDashArray(float linePattern, float lineWeight)
        {
            var w = Math.Max(lineWeight, 1);

            switch ((int)Math.Round(linePattern))
            {
                case 0: return null; // no line
                case 1: return ""; // solid
                case 2: return $"{w * 6} {w * 3}"; // dash
                case 3: return $"{w} {w * 3}"; // dot
                case 4: return $"{w * 6} {w * 3} {w} {w * 3}"; // dash-dot
                case 5: return $"{w * 6} {w * 3} {w} {w * 3} {w} {w * 3}"; // dash-dot-dot
                default: return "";
            }
        }

        private static string FormatFontFamily(string fontFamily)
        {
            if (string.IsNullOrEmpty(fontFamily) || fontFamily == "Themed")
                return "Calibri, Arial, sans-serif";

            var clean = GetSafeXml(fontFamily);

            if (Regex.IsMatch(clean, @","))
                return clean;

            return $"{clean}, Calibri, Arial, sans-serif";
        }

        public static string GeometryToPath(Shape shape, List<Row> rows, float? width, float? height, GeometryToPathResolveOption options = null)
        {
            StringBuilder d = new StringBuilder();

            var curX = 0f; var curY = 0f;
            var startX = 0f; var startY = 0f;

            // If first row is not a MoveTo, add implicit MoveTo(0,0)
            if (rows.Count > 0 && rows[0].Type != "MoveTo" && rows[0].Type != "RelMoveTo")
            {
                d.Append($"M 0 {InchToPixel(height ?? 0)} ");
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var x = row.X != null ? InchToPixel(row.X.Value) : curX;

                // Flip Y: Visio Y-up → SVG Y-down within shape local coords
                var y = row.Y != null ? InchToPixel((height ?? 0) - row.Y.Value) : curY;

                switch (row.Type)
                {
                    case "MoveTo":
                    case "RelMoveTo":
                        {
                            var mx = x; var my = y;

                            if (row.Type == "RelMoveTo" && row.X != null && row.Y != null)
                            {
                                mx = InchToPixel(row.X.Value * width);
                                my = InchToPixel((1 - row.Y.Value) * height);
                            }

                            if (options?.ConnectInternalMoves == true && d.Length > 0)
                            {
                                d.Append($"L {mx} {my} ");
                            }
                            else
                            {
                                d.Append($"M {mx} {my} ");
                            }

                            curX = mx; curY = my;
                            startX = mx; startY = my;

                            break;
                        }

                    case "LineTo":
                    case "RelLineTo":
                        {
                            var lx = x; var ly = y;

                            if (row.Type == "RelLineTo" && row.X != null && row.Y != null)
                            {
                                lx = InchToPixel(row.X.Value * width);
                                ly = InchToPixel((1 - row.Y.Value) * height);
                            }

                            d.Append($"L {lx} {ly} ");

                            curX = lx; curY = ly;

                            break;
                        }

                    case "ArcTo":
                        {
                            // Visio ArcTo: endpoint (X,Y) and bulge A
                            // A is the distance from the arc midpoint to the chord midpoint
                            var bulge = row.A != null ? InchToPixel((float)row.A) : 0;

                            if (Math.Abs(bulge) < 0.001)
                            {
                                // Straight line
                                d.Append($"L {x} {y} ");
                            }
                            else
                            {
                                // Calculate arc from chord and bulge
                                var dx = x - curX;
                                var dy = y - curY;
                                var chordLen = Math.Sqrt(dx * dx + dy * dy);

                                if (chordLen < 0.001f)
                                {
                                    d.Append($"L {x} {y} ");
                                }
                                else
                                {
                                    // radius from bulge and chord
                                    var h = bulge; // sagitta (can be negative)
                                    var r = Math.Abs((chordLen * chordLen / 4 + h * h) / (2 * h));
                                    var largeArc = Math.Abs(h) > chordLen / 2 ? 1 : 0;
                                    var sweep = h > 0 ? 0 : 1;

                                    d.Append($"A {r} {r} 0 {largeArc} {sweep} {x} {y} ");
                                }
                            }

                            curX = x; curY = y;

                            break;
                        }

                    case "EllipticalArcTo":
                        {
                            d.Append(EllipticalArcCommand(row, curX, curY, x, y, width ?? 0, height ?? 0));
                            curX = x; curY = y;

                            break;
                        }

                    case "NURBSTo":
                        {
                            NurbsControlInfo nurbs = ParseNurbsControlPoints(row.E, width ?? 0, height ?? 0);

                            if (nurbs != null)
                            {
                                if (nurbs.Degree == 3 && nurbs.Points.Count >= 2)
                                {
                                    var cp1 = nurbs.Points[0];
                                    var cp2 = nurbs.Points[1];

                                    d.Append($"C {InchToPixel(cp1.X)} {InchToPixel(height - cp1.Y)} {InchToPixel(cp2.X)} {InchToPixel(height - cp2.Y)} {x} {y} ");
                                }
                                else if (nurbs.Points.Count > 0)
                                {
                                    foreach (var point in nurbs.Points)
                                    {
                                        d.Append($"L {InchToPixel(point.X)} {InchToPixel(height - point.Y)} ");
                                    }

                                    d.Append($"L {x} {y} ");
                                }
                                else
                                {
                                    d.Append($"L {x} {y} ");
                                }
                            }

                            curX = x; curY = y;

                            break;
                        }

                    case "PolylineTo":
                        {
                            // Parse POLYLINE formula: POLYLINE(lastX, lastY, x1, y1, x2, y2, ...)
                            if (row.A != null)
                            {
                                Match match = Regex.Match(row.A?.ToString(), @"POLYLINE\(([^)]+)\)", RegexOptions.IgnoreCase);

                                if (match.Success)
                                {
                                    var nums = match.Groups[1].Value.Split(",").Select(s => float.Parse(s.Trim())).ToArray();

                                    // First two are flags/last point, then pairs of x,y
                                    for (var i = 2; i + 1 < nums.Length; i += 2)
                                    {
                                        var px = InchToPixel(nums[i]);
                                        var py = InchToPixel(height - nums[i + 1]);

                                        d.Append($"L {px} {py} ");
                                    }
                                }
                            }

                            d.Append($"L {x} {y} ");

                            curX = x; curY = y;

                            break;
                        }

                    case "SplineStart":
                        {
                            var points = new List<PointInfo>() { new PointInfo() { X = curX, Y = curY }, new PointInfo() { X = x, Y = y } };

                            while (rowIndex + 1 < rows.Count && rows[rowIndex + 1].Type == "SplineKnot")
                            {
                                rowIndex++;

                                var knot = rows[rowIndex];

                                points.Add(new PointInfo()
                                {
                                    X = knot.X != null ? InchToPixel(knot.X.Value) : points[points.Count - 1].X,
                                    Y = knot.Y != null ? InchToPixel(height - knot.Y.Value) : points[points.Count - 1].Y
                                });
                            }

                            d.Append(CatmullRomToBezier(points));

                            var last = points[points.Count - 1];
                            curX = last.X; curY = last.Y;

                            break;
                        }

                    case "SplineKnot":
                        {
                            d.Append($"L {x} {y} ");

                            curX = x; curY = y;

                            break;
                        }

                    case "InfiniteLine":
                        {
                            // Just draw a line segment for display
                            if (row.A != null && row.B != null)
                            {
                                var ax = InchToPixel(row.A);
                                var ay = InchToPixel(height - row.B.Value);

                                d.Append($"M {ax} {ay} L {x} {y} ");
                            }

                            curX = x; curY = y;

                            break;
                        }

                    case "Ellipse":
                        {
                            // Special: defines an ellipse with center (X,Y) and control points A,B,C,D
                            if (row.A != null && row.B != null)
                            {
                                // X,Y = center, A,B = endpoint of semi-major axis
                                var cx = x;
                                var cy = y;
                                var ax = InchToPixel((float)row.A);
                                var ay = InchToPixel(height.Value - row.B.Value);
                                var rx = Math.Sqrt(Math.Pow((ax - cx), 2) + Math.Pow((ay - cy), 2));
                                var ry = rx;

                                if (row.C != null && row.D != null)
                                {
                                    var dx = InchToPixel(row.C.Value);
                                    var dy = InchToPixel(height.Value - row.D.Value);

                                    ry = Math.Sqrt(Math.Pow((dx - cx), 2) + Math.Pow((dy - cy), 2));
                                }

                                // Draw ellipse as two arcs
                                d.Append($"M {cx - rx} {cy} A {rx} {ry} 0 1 0 {cx + rx} {cy} A {rx} {ry} 0 1 0 {cx - rx} {cy} ");
                            }

                            curX = x; curY = y;

                            break;
                        }

                    case "RelCubBezTo":
                        {
                            // Relative cubic bezier (values 0-1 relative to shape)
                            if (row.A != null && row.B != null)
                            {
                                var cp1x = InchToPixel((row.A) * width);
                                var cp1y = InchToPixel((1 - row.B.Value) * (height ?? 0));
                                var cp2x = InchToPixel((row.C ?? row.A) * width);
                                var cp2y = InchToPixel((1 - (row.D ?? row.B)).Value * (height ?? 0));
                                var ex = InchToPixel(row.X.Value * (width ?? 0));
                                var ey = InchToPixel((1 - row.Y.Value) * (height ?? 0));

                                d.Append($"C {cp1x} {cp1y} {cp2x} {cp2y} {ex} {ey} ");

                                curX = ex; curY = ey;
                            }
                            break;
                        }

                    case "RelEllipticalArcTo":
                        {
                            // Relative elliptical arc
                            var ex = InchToPixel(row.X.Value * width);
                            var ey = InchToPixel((1 - row.Y.Value) * height);

                            d.Append(EllipticalArcCommand(row, curX, curY, ex, ey, width ?? 0, height ?? 0, true));

                            curX = ex; curY = ey;

                            break;
                        }

                    case "RelQuadBezTo":
                        {
                            if (row.A != null && row.B != null)
                            {
                                var cpX = InchToPixel(row.A * width);
                                var cpY = InchToPixel((1 - row.B.Value) * height);
                                var ex = InchToPixel(row.X.Value * width);
                                var ey = InchToPixel((1 - row.Y.Value) * height);

                                d.Append($"Q {cpX} {cpY} {ex} {ey} ");

                                curX = ex; curY = ey;
                            }

                            break;
                        }

                    default:
                        // Unknown row type - skip
                        break;
                }
            }

            return d.ToString().Trim();
        }

        private static string EllipticalArcCommand(Row row, float startX, float startY, float endX, float endY, float width, float height, bool relative = false)
        {
            if (row.A == null || row.B == null)
                return $"L {endX} {endY} ";

            var cpX = InchToPixel(relative ? (float)row.A * width : (float)row.A);
            var cpY = InchToPixel(relative ? (1 - row.B.Value) * height : height - row.B.Value);
            var dx = Math.Abs(endX - startX);
            var dy = Math.Abs(endY - startY);
            var rx = Math.Max(Math.Max(dx, Math.Abs(cpX - startX)), Math.Abs(endX - cpX));
            var ry = Math.Max(Math.Max(dy, Math.Abs(cpY - startY)), Math.Abs(endY - cpY));

            float? aspect = row.D.HasValue && Math.Abs(row.D.Value) > 0.001f ? Math.Abs(row.D.Value) : null;

            if (aspect.HasValue)
            {
                if (rx >= ry)
                    ry = rx / aspect.Value;
                else
                    rx = ry * aspect.Value;
            }

            if (rx < 0.001f || ry < 0.001f)
                return $"L {endX} {endY} ";

            var rotation = row.C != null ? -row.C.Value * (180.0 / Math.PI) : 0;
            var v1x = cpX - startX;
            var v1y = cpY - startY;
            var v2x = endX - cpX;
            var v2y = endY - cpY;
            var sweep = (v1x * v2y - v1y * v2x) >= 0 ? 1 : 0;

            return $"A {rx} {ry} {rotation} 0 {sweep} {endX} {endY} ";
        }

        private static NurbsControlInfo ParseNurbsControlPoints(string formula, float width, float height)
        {
            if (string.IsNullOrEmpty(formula))
                return null;

            var match = Regex.Match(formula, @"NURBS\(([^)]*)\)", RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            var values = match.Groups[1].Value.Split(",").Select((part) => part.Trim()).ToArray();

            if (values.Any((value) => !float.TryParse(value, out _)) || values.Length < 8)
                return null;

            var degree = int.Parse(values[1]);
            var xType = int.Parse(values[2]);
            var yType = int.Parse(values[3]);
            var pointValues = values.Skip(4).ToArray();

            var points = new List<PointInfo>();

            for (var i = 0; i + 3 < pointValues.Length; i += 4)
            {
                var rawX = float.Parse(pointValues[i]);
                var rawY = float.Parse(pointValues[i + 1]);

                points.Add(new PointInfo()
                {
                    X = xType == 0 ? rawX * width : rawX,
                    Y = yType == 0 ? rawY * height : rawY
                });
            }

            return new NurbsControlInfo()
            {
                Degree = degree,
                Points = points
            };
        }

        private static string CatmullRomToBezier(List<PointInfo> points)
        {
            if (points.Count < 2)
                return "";

            var d = "";

            for (var i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[Math.Max(0, i - 1)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Math.Min(points.Count - 1, i + 2)];
                var cp1x = p1.X + (p2.X - p0.X) / 6;
                var cp1y = p1.Y + (p2.Y - p0.Y) / 6;
                var cp2x = p2.X - (p3.X - p1.X) / 6;
                var cp2y = p2.Y - (p3.Y - p1.Y) / 6;

                d += $"C {cp1x} {cp1y} {cp2x} {cp2y} {p2.X} {p2.Y} ";
            }

            return d;
        }

        private static bool HasMultipleSubpaths(string pathData)
        {
            var m = Regex.Match(pathData, @"[Mm]");

            return m.Success && m.Groups.Count > 1;
        }

        private static void AddClass(HtmlNode el, string className)
        {
            var existing = el.GetAttributeValue("class", "");

            el.SetAttributeValue("class", !string.IsNullOrEmpty(existing) ? $"{existing} {className}" : className);
        }

        private static float ClampOpacityFromTransparency(float? transparency, float? fallback = 0)
        {
            var value = transparency ?? fallback;

            return Math.Max(0, Math.Min(1, 1 - value.Value));
        }

        private static bool IsRadialGradientPattern(float fillPattern)
        {
            return new int[] { 29, 30, 31, 32, 37, 38, 39 }.Contains((int)Math.Round(fillPattern));
        }

        private void Log(string message, LogType logType = LogType.Info)
        {
            if (this.enableLog == false)
            {
                return;
            }

            if (logType == LogType.Info)
            {
                LogHelper.LogInfo(message);
            }
            else if (logType == LogType.Error)
            {
                LogHelper.LogError(message);
            }
        }
    }
}
