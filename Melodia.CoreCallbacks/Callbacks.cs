namespace Melodia.CoreCallbacks;

using Melodia.Common;
using Steamworks;

public static class Callbacks {
    public static bool Hook_RestartAppIfNecessary(AppId_t unOwnAppID) {
        Log.Trace("Intercepting RestartAppIfNecessary.");
        return false;
    }
}