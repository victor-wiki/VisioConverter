namespace VisioConverter.Extension
{
    public static class FloatExtension
    {
        public static string ToFixed(this float value, int number)
        {
            return value.ToString("0." + string.Join("", Enumerable.Repeat("0", number)));
        }       

        public static bool IsEmpty(this float? value)
        {
            return value == null || value == 0;
        }
    }
}
