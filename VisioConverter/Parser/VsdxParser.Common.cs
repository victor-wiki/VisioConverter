using System.IO.Packaging;
using System.Xml.Linq;
using VisioConverter.Extension;
using VisioConverter.Model;

namespace VisioConverter
{
    public partial class VsdxParser
    {
        private PackagePart GetPackagePart(string uri)
        {
            return this.packageParts.FirstOrDefault(item => item.Uri.OriginalString == uri);
        }

        private string GetFileContent(PackagePart part)
        {
            var stream = part.GetStream();

            using (StreamReader sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }

        private static byte[] GetFileBytes(PackagePart part)
        {
            Stream stream = part.GetStream();

            BinaryReader br = new BinaryReader(stream);

            byte[] bytes = br.ReadBytes((int)stream.Length);

            return bytes;
        }

        private static List<Cell> GetCells(XElement element)
        {
            List<Cell> cells = new List<Cell>();

            var cellElements = element?.Children("Cell");

            if (cellElements != null)
            {
                foreach (var cell in cellElements)
                {
                    cells.Add(GetCell(cell));
                }
            }

            return cells;
        }

        private static Cell GetCell(XElement element)
        {
            var n = element.GetAttributeValue("N");
            var v = element.GetAttributeValue("V");
            var f = element.GetAttributeValue("F");

            return new Cell { Name = n, Value = v, Formula = f };
        }

        private static Cell GetCell(XElement element, string name)
        {
            var cells = GetCells(element);

            return cells.FirstOrDefault(item => item.Name == name);
        }

        private static float? GetCellFloatValue(XElement element, string name)
        {
            return GetCellFloatValue(GetCells(element), name);
        }

        private static float? GetCellFloatValue(Shape shape, string name)
        {
            return GetCellFloatValue(shape.Cells, name);
        }

        private static float? GetCellFloatValue(Row row, string name)
        {
            return GetCellFloatValue(row.Cells, name);
        }

        private static float? GetCellFloatValue(List<Cell> cells, string name)
        {
            var value = GetCellValue(cells, name);

            if (!string.IsNullOrEmpty(value))
            {
                if (float.TryParse(value, out var val))
                {
                    return val;
                }
            }

            return null;
        }

        private static float GetCellNumberValue(List<Cell> cells, string name, float defaultValue = 0.0f)
        {
            var value = GetCellValue(cells, name);

            if (!string.IsNullOrEmpty(value))
            {
                if (float.TryParse(value, out var val))
                {
                    return val;
                }
            }

            return defaultValue;
        }

        private static string GetCellValue(XElement element, string name, string defaultValue = null)
        {
            return GetCellValue(GetCells(element), name, defaultValue);
        }

        private static string GetCellValue(Shape shape, string name, string defaultValue = null)
        {
            return GetCellValue(shape.Cells, name, defaultValue);
        }

        private static string GetCellValue(Row row, string name, string defaultValue = null)
        {
            return GetCellValue(row.Cells, name, defaultValue);
        }

        private static string GetCellValue(List<Cell> cells, string name, string defaultValue = null)
        {
            if (cells == null)
            {
                return null;
            }

            var cell = GetCell(cells, name);

            if (cell != null)
            {
                return cell.Value ?? defaultValue;
            }

            return defaultValue;
        }

        private static string GetCellFormula(List<Cell> cells, string name)
        {
            if (cells == null)
            {
                return null;
            }

            var cell = GetCell(cells, name);

            if (cell != null)
            {
                return cell.Formula;
            }

            return null;
        }

        private static string GetCellUnit(List<Cell> cells, string name)
        {
            if (cells == null)
            {
                return null;
            }

            var cell = GetCell(cells, name);

            if (cell != null)
            {
                return cell.Unit;
            }

            return null;
        }

        private static Cell GetCell(Shape shape, string name)
        {
            return GetCell(shape.Cells, name);
        }

        private static Cell GetCell(Row row, string name)
        {
            return GetCell(row.Cells, name);
        }

        private static Cell GetCell(List<Cell> cells, string name)
        {
            return cells?.FirstOrDefault(item => item.Name == name);
        }

        private static Cell ResolveStyleCellData(List<StyleSheet> styles, string styleId, string cellName, string styleKind, HashSet<string> seen = null)
        {
            if (styles == null || string.IsNullOrEmpty(styleId) || seen?.Contains(styleId) == true)
                return null;

            seen ??= new HashSet<string>();

            seen.Add(styleId);

            var style = styles.FirstOrDefault(item => item.Id == styleId);

            if (style == null)
                return null;

            var cell = style.Cells?.FirstOrDefault(item => item.Name == cellName);

            if (cell != null && cell.Formula != "Inh")
                return cell;

            var parentId = styleKind == "line" ? style.LineStyle
              : styleKind == "fill" ? style.FillStyle
                : style.TextStyle;

            return ResolveStyleCellData(styles, parentId, cellName, styleKind, seen);
        }

        private static Section GetSection(XElement element)
        {
            if (element == null)
            {
                return null;
            }

            var name = element.GetAttributeValue("N");

            Section section = new Section() { Name = name };

            foreach (var row in element.Children("Row"))
            {
                var row_ix = row.GetAttributeValue("IX", "0");
                var row_name = row.GetAttributeValue("N");
                var row_type = row.GetAttributeValue("T");
                var isDelete = row.GetAttributeValue("Del") == "1";

                Row sectionRow = new Row() { Index = row_ix, Name = row_name, Type = row_type, IsDelete = isDelete, Cells = new List<Cell>() };

                foreach (var cell in row.Children("Cell"))
                {
                    sectionRow.Cells.Add(new Cell() { Name = cell.GetAttributeValue("N"), Value = cell.GetAttributeValue("V") });
                }

                section.Rows ??= new List<Row>();

                section.Rows.Add(sectionRow);
            }

            return section;
        }
    }
}
