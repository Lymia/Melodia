namespace Melodia.Common;

/// <summary>
/// The main entry point for patcher plugins.
/// </summary>
public interface Plugin {
    public bool InvalidatesAchievements { get; }

    public void Patch(PatcherContext context);
}
