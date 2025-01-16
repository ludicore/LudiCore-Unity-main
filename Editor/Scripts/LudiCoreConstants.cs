namespace IndieBuff.Editor
{
    static class IndieBuffConstants
    {

        private static bool isLocal = false;

        public static string baseAssetPath = isLocal ? "Assets/IndieBuff" : "Packages/com.indiebuff.aiassistant";
    }
}