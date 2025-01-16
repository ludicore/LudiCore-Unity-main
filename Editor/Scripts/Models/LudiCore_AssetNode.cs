using System;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_AssetNode
    {
        public string Name;
        public string Path;
        public string Type;
        public DateTime LastModified;
        public DateTime Added;
        public float RelevancyScore = 1.0f;
    }
}