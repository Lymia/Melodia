namespace Melodia.CoreCallbacks;

using Melodia.Common;
using Microsoft.Xna.Framework;
using Sang;
using Sang.Gfx;
using Sang.Window;
using Sang.Window.Title;
using Steamworks;

public static class Callbacks {
    public static bool Hook_RestartAppIfNecessary(AppId_t unOwnAppID) {
        Log.Trace("Intercepting RestartAppIfNecessary.");
        return false;
    }

    // TODO: Figure out why the signature here can't just be WindowTitleFooter self
    public static void Hook_WindowTitleFooter_Draw(object self0) {
        var self = (WindowTitleFooter) self0;

        Graphics.SpriteBatchBegin();

        var additionalOffset = 0;
        var plugins = CommonInfo.PluginList;
        for (var i = plugins.Length - 1; i >= 0; i--) {
            var plugin = plugins[i];
            Fonts.Draw(
                FontType.Standard, plugin.Author, 
                new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
                0.75f, Justify.Left
            );
            additionalOffset += 22;
            Fonts.Draw(
                FontType.Standard, $"{plugin.Name} - Version {plugin.Version}", 
                new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
                0.75f, Justify.Left
            );
            additionalOffset += 30;
        }
        Fonts.Draw(
            FontType.Standard, GameSystem.COPYRIGHT_FULL, 
            new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
            0.75f, Justify.Left
        );
        Fonts.Draw(
            FontType.Standard, $"Crystal Project - {GameSystem.VersionString}", 
            new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - 22 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
            0.75f, Justify.Left
        );

        Graphics.SpriteBatch.End();
    }
}
