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
            internal Dictionary<object, object> Dict = new Dictionary<object, object>();
        }
    }

    public static class CommonInfo {
        public static string BaseDirectory => InternalApi.InternalCommonInfo.BaseDirectory;
        public static string GameDirectory => InternalApi.InternalCommonInfo.GameDirectory;
        public static string TempDirectory => InternalApi.InternalCommonInfo.TempDirectory;
        public static Dictionary<object, object> CommonDict => 
            (InternalApi.InternalCommonInfo.DataStore ?? throw new Exception("No CommonDict available?")).Dict;
    }
}
