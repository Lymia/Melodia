namespace Melodia.CoreCallbacks.PrivateImpl;

using System.IO;
using Melodia.Common;
using Melodia.Common.Plugin;
using Sang;
using Steamworks;

public static class Callbacks {
    public static bool Hook_RestartAppIfNecessary(AppId_t unOwnAppID) {
        Log.Trace("Intercepting RestartAppIfNecessary.");
        return false;
    }
}

[Patch("Sang.GameMain")]
public sealed class GameMainHook {
    public GameMainHook(GameMain self) {
        Log.Trace("Overriding ContentManager directory");
        self.Content.RootDirectory = $"{Path.Combine(CommonInfo.GameDirectory, "Content")}";
    }
}
