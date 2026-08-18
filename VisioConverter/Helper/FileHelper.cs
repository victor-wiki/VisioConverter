namespace VisioConverter.Helper
{
    public class FileHelper
    {
        public static readonly Dictionary<string, string> IMAGE_MIME_TYPES = new Dictionary<string, string>()
        {
             {".png", "image/png"},
             {".jpg", "image/jpeg"},
             {".jpeg", "image/jpeg"},
             {".gif", "image/gif"},
             {".bmp", "image/bmp"},
             {".svg", "image/svg+xml"},
             {".tif", "image/tiff"},
             {".tiff", "image/tiff" }
        };

        public static string GetImageBase64String(byte[] bytes, string filename)
        {
            string ext = Path.GetExtension(filename).ToLower();

            var mime = IMAGE_MIME_TYPES.ContainsKey(ext) ? IMAGE_MIME_TYPES[ext] : "image/png";

            return $"data:{mime};base64,{BytesToBase64(bytes)}";
        }

        public static string BytesToBase64(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }
    }
}
