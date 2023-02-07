using System;
using System.Collections.Generic;

namespace Melodia.Common {
    namespace InternalApi {
        public static class InternalCommonInfo {
            public static string BaseDirectory = "";
            public static string GameDirectory = "";
            public static string TempDirectory = "";
            public static InternalCommonDataStore? DataStore = null;
        }

        public sealed class InternalCommonDataStore : PersistantRemoteObject {
            internal Dictionary<object, object> CommonDict = new Dictionary<object, object>();

            public PluginInfo[] PluginList = new PluginInfo[] {};
        }
    }

    [Serializable]
    public readonly record struct PluginInfo(string Name, string Version, string Author);

    public static class CommonInfo {
        public static string BaseDirectory => InternalApi.InternalCommonInfo.BaseDirectory;
        public static string GameDirectory => InternalApi.InternalCommonInfo.GameDirectory;
        public static string TempDirectory => InternalApi.InternalCommonInfo.TempDirectory;

        private static InternalApi.InternalCommonDataStore dataStore => 
            InternalApi.InternalCommonInfo.DataStore ?? throw new Exception("No CommonDict available?");
        public static Dictionary<object, object> CommonDict => dataStore.CommonDict;
        public static PluginInfo[] PluginList => dataStore.PluginList;
    }
}
