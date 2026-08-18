using System.Runtime.CompilerServices;

namespace VisioConverter.Extension
{
    public static class DictionaryExtension
    {
        public static bool HasValue(this Dictionary<string, string> dict, string key)
        {
            if(dict == null)
            {
                return false;
            }

            if(dict.ContainsKey(key))
            {
                return !string.IsNullOrEmpty(dict[key]);
            }

            return false;
        }

        public static string GetValue(this Dictionary<string, string> dict, string key)
        {
            if (dict == null)
            {
                return null;
            }

            if (dict.ContainsKey(key))
            {
                return dict[key];
            }

            return null;
        }
    }
}
