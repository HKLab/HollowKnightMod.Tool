﻿
namespace HKTool;
public abstract class ModBase : Mod
{
    private static FsmFilter CreateFilter(FsmPatcherAttribute attr)
    {
        if (attr.useRegex)
        {
            return new FsmNameFilterRegex(string.IsNullOrEmpty(attr.sceneName) ? null : new Regex(attr.sceneName),
            string.IsNullOrEmpty(attr.objName) ? null : new Regex(attr.objName),
            string.IsNullOrEmpty(attr.fsmName) ? null : new Regex(attr.fsmName));
        }
        else
        {
            return new FsmNameFilter(attr.sceneName, attr.objName, attr.fsmName);
        }
    }
    protected virtual bool ShowDebugView => true;
    public override string GetVersion()
    {
        return GetType().Assembly.GetName().Version.ToString();
    }
    public virtual I18n I18n => _i18n.Value;
    public byte[] GetEmbeddedResource(string name) => EmbeddedResHelper.GetBytes(GetType().Assembly, name);
    public Texture2D LoadTexture2D(string name)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.LoadImage(GetEmbeddedResource(name));
        return tex;
    }
    public AssetBundle LoadAssetBundle(string name)
    {
        return AssetBundle.LoadFromMemory(GetEmbeddedResource(name));
    }

    public ModBase(string name = null) : base(name)
    {
        ModManager.NewMod(this);
        if (this is IDebugViewBase @base && ShowDebugView)
        {
            DebugView.debugViews.Add(@base);
        }
        foreach (var v in GetType().GetRuntimeMethods())
        {
            if (v.ReturnType != typeof(void) || !v.IsStatic
                || v.GetParameters().Length != 1 || v.GetParameters().FirstOrDefault()?.ParameterType != typeof(FSMPatch)) continue;

            var d = (WatchHandler<FSMPatch>)v.CreateDelegate(typeof(WatchHandler<FSMPatch>));
            foreach (var attr in v.GetCustomAttributes<FsmPatcherAttribute>())
            {
                new FsmWatcher(CreateFilter(attr), d);
            }
        }
        _i18n = new Lazy<I18n>(
            () => new(GetName(), Path.GetDirectoryName(GetType().Assembly.Location))
        );
        var l = Languages;
        if (l != null)
        {
            Assembly ass = GetType().Assembly;
            foreach (var v in l)
            {
                try
                {
                    using (Stream stream = ass.GetManifestResourceStream(v.Item2))
                    {
                        I18n.AddLanguage(v.Item1, stream, false);
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
            I18n.UseGameLanguage(DefaultLanguageCode, true);
        }
    }
    private readonly Lazy<I18n> _i18n;
    protected virtual (Language.LanguageCode, string)[] Languages => null;
    protected virtual Language.LanguageCode DefaultLanguageCode => Language.LanguageCode.EN;
    public virtual string GetViewName() => GetName();
    public virtual bool FullScreen => false;

    public virtual void OnDebugDraw()
    {
        GUILayout.Label("Empty DebugView");
    }
}
public abstract class ModBase<TGlobalSettings, TLocalSettings> : ModBase where TGlobalSettings : new()
        where TLocalSettings : new()
{
    public virtual TLocalSettings localSettings { get; protected set; } = new();
    public TLocalSettings OnSaveLocal() => localSettings;
    public void OnLoadLocal(TLocalSettings s) => localSettings = s;
    public virtual TGlobalSettings globalSettings { get; protected set; } = new();
    public TGlobalSettings OnSaveGlobal() => globalSettings;
    public void OnLoadGlobal(TGlobalSettings s) => globalSettings = s;
}

