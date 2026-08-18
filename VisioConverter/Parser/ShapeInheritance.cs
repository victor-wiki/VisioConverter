using System.Text.RegularExpressions;
using VisioConverter.Helper;
using VisioConverter.Model;

namespace VisioConverter.Parser
{
    public class ShapeInheritance
    {
        // Character-style fields that we try to inherit from a master shape when the
        // child shape does not specify them explicitly.
        public static readonly string[] CHAR_FIELDS = ["FontSize", "FontFamily", "FontColor", "Bold", "Italic"];

        /// <summary>
        /// Merge `masterShape` into `shape` in-place: any field on `shape` that is
        /// null / undefined / empty is populated from the master. The caller is
        /// responsible for pre-merging sub-shape data; we only do shallow merging of
        /// the normalized text / style properties.
        /// 
        /// Fields merged: text, fontSize, fontColor, bold, italic, and any value in
        /// `propMap` / `userMap` (for later field resolution) that the shape is missing.
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="masterShape"></param>
        /// <returns></returns>
        public static Shape InheritFromMaster(Shape shape, Shape masterShape)
        {
            if (shape == null || masterShape == null)
                return shape;

            // Text: only inherit when the shape has no text of its own.
            if (string.IsNullOrEmpty(shape.Text) && !string.IsNullOrEmpty(masterShape.Text))
            {
                //shape.Text = masterShape.Text; //???

                // When we inherit the raw text we also want the master's field list to
                // drive resolution, because the U+FFFC placeholders refer to the master's
                // FIELD_LIST in the .vsd format, or to <fld IX=...> indices that are
                // defined on the master in .vsdx.
                if (masterShape.Fields != null && shape.Fields == null)
                {
                    shape.Fields = masterShape.Fields;
                }
            }

            // Character style. Bold/italic use a boolean "false is also a default", so
            // we only overwrite when the shape field is strictly null/undefined.
            foreach (var f in CHAR_FIELDS)
            {
                if (ObjectHelper.GetValue(shape, f) == null)
                {
                    var value = ObjectHelper.GetValue(masterShape, f);

                    if (value != null)
                    {
                        ObjectHelper.SetValue(shape, f, value);
                    }
                }
            }

            // Property/user maps: merge any keys the shape is missing, so that a shape
            // that inherited its text from the master (and therefore references the
            // master's custom-property names) can still resolve them through its own
            // page-scoped map. We DO NOT overwrite an existing key — the shape's value
            // wins.
            if (masterShape.PropertySectionMap != null)
            {
                shape.PropertySectionMap ??= new Dictionary<string, string>();

                foreach (var k in masterShape.PropertySectionMap)
                {
                    if (!shape.PropertySectionMap.ContainsKey(k.Key))
                        shape.PropertySectionMap[k.Key] = masterShape.PropertySectionMap[k.Key];
                }
            }

            if (masterShape.UserSectionMap != null)
            {
                shape.UserSectionMap ??= new Dictionary<string, string>();

                foreach (var k in masterShape.UserSectionMap)
                {
                    if (!shape.UserSectionMap.ContainsKey(k.Key))
                        shape.UserSectionMap[k.Key] = masterShape.UserSectionMap[k.Key];
                }
            }

            return shape;
        }

        public static string resolveReference(string reference, FieldResolveContext ctx)
        {
            if (reference == null)
                return null;

            string r = reference.Trim();

            // Prop.X / Prop.Row_1 / Prop."My Prop"
            var m = Regex.Match(r, @"^Prop(?:erty)?\.(.+)$", RegexOptions.IgnoreCase);

            if (m.Success)
            {
                var name = Regex.Replace(m.Value, @"^""(.*)""$", "$1");

                if (ctx.PropertySectinMap != null && ctx.PropertySectinMap.ContainsKey(name))
                    return ctx.PropertySectinMap[name];

                // Strip leading "Row_" numeric suffix matching
                if (ctx.PropertySectinMap != null)
                {
                    // Case-insensitive fallback
                    var lower = name.ToLower();

                    foreach (var k in ctx.PropertySectinMap)
                    {
                        if (k.Key.ToLower() == lower)
                            return ctx.PropertySectinMap[k.Key];
                    }
                }

                return null;
            }

            // User.X
            m = Regex.Match(r, @"^User\.(.+)$", RegexOptions.IgnoreCase);

            if (m.Success)
            {
                var name = Regex.Replace(m.Value, @"^""(.*)""$", "$1");

                if (ctx.UserSectionMap != null && ctx.UserSectionMap.ContainsKey(name))
                    return ctx.UserSectionMap[name];

                if (ctx.UserSectionMap != null)
                {
                    var lower = name.ToLower();

                    foreach (var k in ctx.UserSectionMap)
                    {
                        if (k.Key.ToLower() == lower)
                            return ctx.UserSectionMap[k.Key];
                    }
                }

                return null;
            }

            // Page-level references
            if (Regex.IsMatch(r, @"^PageName$", RegexOptions.IgnoreCase) || Regex.IsMatch(r, @"^ThePage!?PageName$", RegexOptions.IgnoreCase))
                return ctx.PageName;

            if (Regex.IsMatch(r, @"^PageNumber$", RegexOptions.IgnoreCase) || Regex.IsMatch(r, @"^ThePage!?PageNumber$", RegexOptions.IgnoreCase))
            {
                return ctx.PageNumber != null ? ctx.PageNumber.ToString() : null;
            }

            // TEXT("literal") — just unquote
            m = Regex.Match(r, @"^TEXT\(\s*""(.*)""\s*\)$", RegexOptions.IgnoreCase);

            if (m.Success)
                return m.Value;

            m = Regex.Match(r, @"^TEXT\(\s*'(.*)'\s*\)$", RegexOptions.IgnoreCase);

            if (m.Success)
                return m.Value;

            return null;
        }

        /// <summary>
        /// Replace U+FFFC placeholders in a raw text string with the resolved values of
        /// the corresponding fields. Each placeholder is consumed in-order; any field
        /// we cannot resolve falls back to its `value` (if the parser pre-resolved it),
        /// then to its `format` (printf-ish display string), then to an empty string
        /// so we do not render the replacement-character glyph.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public static string SpliceObjectReplacements(string text, FieldResolveContext ctx)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var fields = ctx?.Fields;
            var idx = 0;
            var _out = new List<string>();

            foreach (var ch in text)
            {
                if (ch == '\uFFFC')
                {
                    var f = fields[idx++];
                    string replacement = null;

                    if (f != null)
                    {
                        if (f.Value != null && f.Value != "")
                        {
                            replacement = f.Value;
                        }
                        else if (f.Ref)
                        {
                            replacement = resolveReference(f.Ref, ctx);
                        }

                        if ((replacement == null || replacement == "") && f.Format)
                        {
                            replacement = f.Format;
                        }
                    }

                    if (!string.IsNullOrEmpty(replacement))
                        _out.Add(replacement);
                    // else: drop the placeholder silently.
                }
                else
                {
                    _out.Add(ch.ToString());
                }
            }

            return string.Join("", _out);
        }

        /// <summary>
        /// Replace <fld IX='N'/> XML-style placeholders in a raw text string. Visio's
        /// .vsdx text may contain inline <fld> elements that the parser flattens to
        /// the literal tag text; we also accept the tag-stripped form where the parser
        /// has already substituted the <fld> with a sentinel "\uFFFC" character.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public static string SpliceFieldTags(string text, FieldResolveContext ctx)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Pattern matches both <fld IX='3'/> and <fld IX="3"/>.
            return Regex.Replace(text, @"<fld\s+[^>]*IX\s*=\s*['""](\d+)['""][^>]*\/?>(?:\s*<\/fld>)?",
            match =>
            {
                int ix = match.Index;

                var fields = ctx?.Fields ?? new List<dynamic>();

                if (ix < fields.Count)
                {
                    var f = fields[ix];
                    if (f == null)
                        return string.Empty;

                    string value = ObjectHelper.GetValue(f, "Value")?.ToString();
                    string reference = ObjectHelper.GetValue(f, "Ref")?.ToString();
                    string format = ObjectHelper.GetValue(f, "Format")?.ToString();

                    if (!string.IsNullOrEmpty(value))
                        return value;

                    if (!string.IsNullOrEmpty(reference))
                    {
                        string resolved = resolveReference(reference, ctx);

                        if (!string.IsNullOrEmpty(resolved))
                            return resolved;
                    }

                    if (!string.IsNullOrEmpty(format))
                        return format;
                }

                return string.Empty;
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Main entry: take a raw text string and a context, return the rendered string
        /// with both U+FFFC placeholders and <fld> tags replaced.
        /// </summary>
        /// <param name="shape"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public static string ResolveFields(Shape shape, FieldResolveContext ctx)
        {
            var raw = shape?.Text;

            if (string.IsNullOrEmpty(raw))
                return null;

            // Gather default ctx from the shape itself when the caller did not supply
            // an overriding value (propMap/userMap often live on the shape).
            var merged = new FieldResolveContext()
            {
                PropertySectinMap = ctx?.PropertySectinMap ?? shape.PropertySectionMap,
                UserSectionMap = ctx?.UserSectionMap ?? shape.UserSectionMap,
                PageName = ctx?.PageName,
                PageNumber = ctx?.PageNumber,
                Fields = ctx?.Fields ?? shape?.Fields
            };

            var result = raw;

            if (result.Contains('\uFFFC'))
            {
                result = SpliceObjectReplacements(result, merged);
            }

            if (result.Contains("<fld"))
            {
                result = SpliceFieldTags(result, merged);
            }

            return result;
        }
    }
}
