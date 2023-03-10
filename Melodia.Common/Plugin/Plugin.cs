namespace Melodia.Common.Plugin;

public interface IPlugin {
    public string DisplayName { get; }

    public string DisplayVersion { get; }

    public string DisplayAuthor { get; }

    public bool InvalidatesAchievements { get; }

    public void AfterPatch();

    public void AfterPatchEarly();

    public void BeforePatch();

    public void BeforePatchEarly();

    public void Init();

    public void InitEarly();

    public void Patch(PatcherContext context);

}

/// <summary>
/// The main entry point for patcher plugins.
/// </summary>
public abstract class Plugin : IPlugin {
    public abstract string DisplayName { get; }

    public abstract string DisplayVersion { get; }

    public abstract string DisplayAuthor { get; }

    public virtual bool InvalidatesAchievements => true;

    public virtual void AfterPatch() {}

    public virtual void AfterPatchEarly() {}

    public virtual void BeforePatch() {}

    public virtual void BeforePatchEarly() {}

    public virtual void Init() {}

    public virtual void InitEarly() {}

    public abstract void Patch(PatcherContext context);
}
