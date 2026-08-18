using System.Xml.Linq;

namespace VisioConverter.Model
{
    public class Shape
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NameU { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string Text { get; set; }
        public string MasterId { get; set; }
        public string MasterShapeId { get; set; }
        public List<Cell> Cells { get; set; }
        public string LineStyle { get; set; }
        public string FillStyle { get; set; }
        public string TextStyle { get; set; }      
        public List<Section> Geometries { get; set; }
        public Dictionary<string, FontInfo> CharacterFormats { get; set; }
        public Section ParagraphFormats { get; set; }
        public StyleInfo StyleInfo { get; set; }
        public Shape MasterShape { get; set; }
        public List<Shape> SubShapes { get; set; }
        public XElement Element { get; set; }
        public float? PinX { get; set; }
        public float? PinY { get; set; }
        public float? LocPinX { get; set; }
        public float? LocPinY { get; set; }
        public float? TextPinX { get; set; }
        public float? TextPinY { get; set; }
        public float? Width { get; set; }
        public float? Height { get; set; }
        public float? TextWidth { get; set; }
        public float? TextHeight { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public float? Angle { get; set; }
        public string LineColor { get; set; }
        public float? LineWeight { get; set; }
        public float? LinePattern { get; set; }
        public string FillForeground { get; set; }
        public string FillBackground { get; set; }
        public float? FillForegroundTrans { get; set; }
        public float FillBackgroundTrans { get; set; }
        public float FillPattern { get; set; }
        public float? FillGradientDir { get; set; }
        public List<GradientStop> FillGradientStops { get; set; }
        public ImageDataInfo Image { get; set; }
        public float Rounding { get; set; }
        public float? BeginArrow { get; set; }
        public float? EndArrow { get; set; }
        public float? BeginX { get; set; }
        public float? BeginY { get; set; }
        public float? EndX { get; set; }
        public float? EndY { get; set; }
        public string ObjectType { get; set; }
        public bool Is1D { get; set; }
        public float? FontSize { get; set; }
        public string FontFamily { get; set; }
        public string FontColor { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public List<TextInfo> TextRuns { get; set; }
        public string[] LayerMembers { get; set; }
        public List<UserDef> UserDefs { get; set; }
        public Dictionary<string, string> PropertySectionMap { get; set; }
        public Dictionary<string, string> UserSectionMap { get; set; }
        public CustomPropertyInfo[] CustomPropertyInfos { get; set; }
        public List<dynamic> Fields { get; set; }

        public bool HasGeometry => this.Geometries != null && this.Geometries.Count > 0;
        public bool HasUserDef => this.UserDefs != null && this.UserDefs.Count > 0;
        public bool HasTextRun => this.TextRuns != null && this.TextRuns.Count > 0;
        public bool HasCharacterFormat => this.CharacterFormats != null && this.CharacterFormats.Count > 0;
        public bool HasParagraphFormat => this.ParagraphFormats != null;
        public bool HasSubShape => this.SubShapes != null && this.SubShapes.Count > 0;
        public bool HasLayerMember => this.LayerMembers != null && this.LayerMembers.Length > 0;
        public bool HasFillGradientStop => this.FillGradientStops != null && this.FillGradientStops.Count > 0;
        public bool HasCustomProperty => this.CustomPropertyInfos != null && this.CustomPropertyInfos.Length > 0;
    }
}
