using Melodia.Common;
using Melodia.Common.Plugin;
using Microsoft.Xna.Framework;
using Sang;
using Sang.Gfx;
using Sang.Window;
using Sang.Window.Title;

namespace Melodia.CoreCallbacks {
    public static class TitleHooks {
        public static string Subtitle = "Modded Blah Blah >w<";
    }

    namespace PrivateImpl {
        [PatchComponent]
        public abstract class TitleHook {
            internal abstract float Visibility { get; }

            [CallAfter("Draw")]
            public void Hook_SubtitleDraw() {
                Graphics.SpriteBatchBegin();
                Fonts.Draw(
                    FontType.Standard, TitleHooks.Subtitle, 
                    new Vector2(CAnchor.Middle.X, CAnchor.Middle.Y - 30), CWindow.COLOR_GUI_LIGHT * Visibility, 
                    2.0f, Justify.Center
                );
                Graphics.SpriteBatch.End();
            }
        }

        [Patch("Sang.Window.Title.WindowTitleFooter")]
        public sealed class WindowTitleFooterHook : TitleHook {
            private readonly WindowTitleFooter self;
            private readonly PluginInfo[] plugins;
            internal override float Visibility { get => self._visibility; }

            public WindowTitleFooterHook(WindowTitleFooter self) {
                this.self = self;
                plugins = CommonInfo.PluginList;
            }

            [ReplaceMethod("Draw", CallBase = true)]
            public void Hook_Draw() {
                Graphics.SpriteBatchBegin();

                var additionalOffset = 0;
                for (var i = plugins.Length - 1; i >= 0; i--) {
                    var plugin = plugins[i];
                    Fonts.Draw(
                        FontType.Standard, plugin.Author, 
                        new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
                        0.75f, Justify.Left
                    );
                    additionalOffset += 23;
                    Fonts.Draw(
                        FontType.Standard, $"{plugin.Name} - Version {plugin.Version}", 
                        new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
                        0.75f, Justify.Left
                    );
                    additionalOffset += 32;
                }
                Fonts.Draw(
                    FontType.Standard, GameSystem.COPYRIGHT_FULL, 
                    new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
                    0.75f, Justify.Left
                );
                Fonts.Draw(
                    FontType.Standard, $"Crystal Project - {GameSystem.VersionString}", 
                    new Vector2(CAnchor.BotLeft.X, (CAnchor.BotLeft.Y - 42 - 23 - additionalOffset)), CWindow.COLOR_GUI_GRAY * self._visibility, 
                    0.75f, Justify.Left
                );
                
                Graphics.SpriteBatch.End();
            }
        }

        [Patch("Sang.Window.Title.WindowTitlePressStart")]
        public sealed class WindowTitlePressStartHook : TitleHook {
            private readonly WindowTitlePressStart self;
            internal override float Visibility { get => self._firstTimeVisibility * self._textVisibility * self._visibility; }

            public WindowTitlePressStartHook(WindowTitlePressStart self) {
                this.self = self;
            }
        }
    }
}