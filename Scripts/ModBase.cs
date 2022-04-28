﻿
namespace HKTool;
public abstract class ModBase : Mod, IHKToolMod
{
    [AttributeUsage(AttributeTargets.Method)]
    internal protected class PreloadSharedAssetsAttribute : Attribute
    {
        public PreloadSharedAssetsAttribute(string name, Type? type = null)
        {
            inResources = true;
            this.name = name;
            this.targetType = type ?? typeof(GameObject);
        }
        public PreloadSharedAssetsAttribute(string sceneName, string name, Type? type = null) : this(name, type)
        {
            inResources = false;
            this.sceneName = sceneName;
        }
        public PreloadSharedAssetsAttribute(int id, string name, Type? type = null) : this(name, type)
        {
            inResources = false;
            this.id = id;
        }
        public Type targetType;
        public string sceneName = "";
        public string name;
        public bool inResources;
        public int? id = null;
    }
    public const string compileVersion = "1.7.0.0";
    private static int _currentmapiver = (int)FindFieldInfo("Modding.ModHooks::_modVersion").GetValue(null);
    public static int CurrentMAPIVersion => _currentmapiver;
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
    public virtual string MenuButtonName => GetName();
    public virtual Font MenuButtonLabelFont => MenuResources.TrajanBold;
    public virtual Version? HKToolMinVersion
    {
        get
        {
            var ver = GetType().Assembly.GetCustomAttribute<NeedHKToolVersionAttribute>()?.version;
            if (ver == null) return null;
            return Version.Parse(ver);
        }
    }
    public virtual List<(string, string)>? needModules => null;
    public MenuButton? ModListMenuButton { get; private set; }
    protected virtual bool ShowDebugView => true;
    private void CheckHKToolVersion(string? name = null)
    {
        if (HKToolMinVersion is null) return;
        var hkv = typeof(HKToolMod).Assembly.GetName().Version;
        if (hkv < HKToolMinVersion)
        {
            TooOldDependency("HKTool", HKToolMinVersion);
        }
    }
    public override string GetVersion()
    {
        return GetType().Assembly.GetName().Version.ToString() + "-" + sha1 +
            (DebugManager.IsDebug(this) ? "-Dev" : "");
    }
    public void HideButtonInModListMenu()
    {
        if (ModListMenuButton is null) throw new InvalidOperationException();
        ModListMenuButton.gameObject.SetActive(false);
        ModListMenuHelper.RearrangeButtons();
    }
    public void ShowButtonInModListMenu()
    {
        if (ModListMenuButton is null) throw new InvalidOperationException();
        ModListMenuButton.gameObject.SetActive(true);
        ModListMenuHelper.RearrangeButtons();
    }
    protected void MissingDependency(string name)
    {
        var err = "HKTool.Error.NeedLibrary"
                .GetFormat(name);
        LogError(err);
        ModManager.modErrors.Add((GetName(), err));
        throw new NotSupportedException(err);
    }
    protected void TooOldDependency(string name, Version needVersion)
    {
        var err = "HKTool.Error.NeedLibraryVersion"
                .GetFormat(name, needVersion.ToString());
        LogError(err);
        ModManager.modErrors.Add((GetName(), err));
        throw new NotSupportedException(err);
    }
    public virtual void OnCheckDependencies()
    {

    }
    protected virtual void AfterCreateModListButton(MenuButton button)
    {
        var labelT = button.GetLabelText();
        if (labelT is null) return;
        labelT.text = MenuButtonName;
        labelT.font = MenuButtonLabelFont;
    }
    public virtual I18n I18n => _i18n.Value;
    public byte[]? GetEmbeddedResource(string name) => EmbeddedResHelper.GetBytes(GetType().Assembly, name);
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

    public static ModBase? FindMod(Type type)
    {
        if (ModManager.instanceMap.TryGetValue(type, out var v)) return v;
        return null;
    }

    public readonly string sha1;
    protected static bool HaveAssembly(string name)
    {
        foreach (var v in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (v.GetName().Name == name) return true;
        }
        return false;
    }
    protected void CheckAssembly(string name, Version minVer)
    {
        foreach (var v in AppDomain.CurrentDomain.GetAssemblies())
        {
            var n = v.GetName();
            if (n.Name == name)
            {
                if (n.Version < minVer) TooOldDependency(name, minVer);
                return;
            }
        }
        MissingDependency(name);
    }
    private byte[] GetAssemblyBytes()
    {
        return File.ReadAllBytes(GetType().Assembly.Location);
    }
    private void LoadPreloadResource(List<(string, Type, MethodInfo)> table)
    {
        var batch = new Dictionary<Type, List<(string, MethodInfo)>>();
        foreach (var v2 in table)
        {
            if (!batch.TryGetValue(v2.Item2, out var v3))
            {
                v3 = new();
                batch.Add(v2.Item2, v3);
            }
            v3.Add((v2.Item1, v2.Item3));
        }
        foreach (var g in batch)
        {
            var type = g.Key;
            var objects = Resources.FindObjectsOfTypeAll(type);
            var list = g.Value;
            if (type == typeof(GameObject))
            {
                foreach (var go in (GameObject[])objects)
                {
                    if (go is null) continue;
                    if (go.scene.IsValid() || go.transform.parent != null) continue;
                    var match = list.FirstOrDefault(x => x.Item1 == go.name);
                    if (match.Item2 is not null)
                    {
                        try
                        {
                            match.Item2.FastInvoke(this, go);
                        }
                        catch (Exception e)
                        {
                            LogError(e);
                        }
                        list.Remove(match);
                    }
                }
            }
            else
            {
                foreach (var o in objects)
                {
                    if (o is null) continue;
                    var match = list.FirstOrDefault(x => x.Item1 == o.name);
                    if (match.Item2 is not null)
                    {
                        try
                        {
                            match.Item2.FastInvoke(this, o);
                        }
                        catch (Exception e)
                        {
                            LogError(e);
                        }
                        list.Remove(match);
                    }
                }
            }
            foreach (var v4 in list)
            {
                try
                {
                    v4.Item2.FastInvoke(this, null);
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

    }
    void IHKToolMod.HookInit(Dictionary<string, Dictionary<string, GameObject>> go)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        foreach (var v in go)
        {
            foreach (var v2 in v.Value)
            {
                if (v2.Value.name == "FakeGameObject")
                {
                    UObject.Destroy(v2.Value);
                }
            }
        }
        if (assetpreloads.TryGetValue("InResource", out var inresources))
        {
            LoadPreloadResource(inresources);
        }
        foreach (var v in preloads)
        {
            if (!go.TryGetValue(v.Value.Item1, out var scene))
            {
                if (v.Value.Item3) throw new MissingPreloadObjectException();
                LogWarn("Missing Scene: " + v.Value.Item1);
                continue;
            }
            if (!scene.TryGetValue(v.Value.Item2, out var obj))
            {
                if (v.Value.Item3) throw new MissingPreloadObjectException();
                LogWarn("Missing Object: " + v.Value.Item2);
                continue;
            }
            try
            {
                v.Key.FastInvoke(this, obj);
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }
    }
    private List<(string, string)> HookGetPreloads(List<(string, string)> preloads)
    {
        preloads = preloads ?? new();
        foreach (var v in this.preloads) preloads.Add((v.Value.Item1, v.Value.Item2));
        foreach (var v in assetpreloads) preloads.Add((v.Key, "FakeGameObject"));
        return preloads;
    }
    private void CheckHookGetPreloads()
    {
        if (needHookGetPreloads)
        {
            var gpM = GetType().GetTypeInfo().DeclaredMethods.FirstOrDefault(x => x.Name == "GetPreloadNames"
                && x.GetParameters().Length == 0 &&
                x.ReturnType == typeof(List<(string, string)>));
            if (gpM is not null)
            {
                HookEndpointManager.Add(
                    gpM,
                    (Func<ModBase, List<(string, string)>> orig, ModBase self) =>
                    {
                        return HookGetPreloads(orig(self));
                    }
                );
            }
            else
            {
                ModManager.hookGetPreloads[this] = HookGetPreloads;
            }
            if (assetpreloads.Count != 0)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode _1)
    {
        if (assetpreloads.TryGetValue(scene.name, out var v))
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(new GameObject("FakeGameObject"), scene);
            LoadPreloadResource(v);
        }
    }
    private bool needHookGetPreloads = false;
    private Dictionary<MethodInfo, (string, string, bool)> preloads = new();
    private Dictionary<string, List<(string, Type, MethodInfo)>> assetpreloads = new();
    private void CheckPreloads()
    {
        var t = GetType();
        foreach (var v in t.GetMethods(HReflectionHelper.All))
        {
            var p = v.GetCustomAttribute<PreloadAttribute>();
            if (p is not null)
            {
                preloads.Add(v, (p.sceneName, p.objPath, p.throwExceptionOnMissing));
                needHookGetPreloads = true;
                continue;
            }
            var pa = v.GetCustomAttribute<PreloadSharedAssetsAttribute>();
            if (pa is not null)
            {
                string scene = pa.inResources ? "InResource"
                    : (pa.id is null ? pa.sceneName :
                    Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(pa.id ?? throw new NullReferenceException())));
                if (!assetpreloads.TryGetValue(scene, out var list))
                {
                    list = new();
                    assetpreloads.Add(scene, list);
                }
                needHookGetPreloads = true;
                list.Add((pa.name, pa.targetType, v));
            }
        }
    }
    public ModBase(string? name = null) : base(name)
    {
        CheckHKToolVersion(name);
        OnCheckDependencies();
        ModManager.NewMod(this);

        sha1 = BitConverter.ToString(
            System.Security.Cryptography.SHA1
                .Create().ComputeHash(GetAssemblyBytes()))
                .Replace("-", "").ToLowerInvariant().Substring(0, 6);

        if (this is ICustomMenuMod || this is IMenuMod)
        {
            ModListMenuHelper.OnAfterBuildModListMenuComplete += (_) =>
            {
                ModListMenuButton = ModListMenuHelper.FindButtonInMenuListMenu(GetName());
                if (ModListMenuButton is null) return;
                var buttonText = ModListMenuButton.GetLabelText();
                if (buttonText is not null)
                {
                    buttonText.text = MenuButtonName;
                    buttonText.font = MenuButtonLabelFont;
                }
                AfterCreateModListButton(ModListMenuButton);
            };
        }

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
                    () =>
                        new(GetName(), Path.GetDirectoryName(GetType().Assembly.Location), (LanguageCode)DefaultLanguageCode)
                );
        InitI18n();
        CheckPreloads();
        CheckHookGetPreloads();
    }
    private void InitI18n()
    {

#pragma warning disable CS0618
        var l = Languages;
        if (l != null)
        {
            Assembly ass = GetType().Assembly;
            foreach (var v in l)
            {
                try
                {
                    using (Stream stream = EmbeddedResHelper.GetStream(ass, v.Item2))
                    {
                        I18n.AddLanguage(v.Item1, stream, false);
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }
#pragma warning restore CS0618
        var lex = LanguagesEx;
        if (lex != null)
        {
            Assembly ass = GetType().Assembly;
            foreach (var v in lex)
            {
                try
                {
                    using (Stream stream = EmbeddedResHelper.GetStream(ass, v.Item2))
                    {
                        I18n.AddLanguage(v.Item1, stream, false);
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }
        if (l is not null || lex is not null)
        {
            I18n.TrySwitch();
            if (this is ICustomMenuMod || this is IMenuMod)
            {
                I18n.OnLanguageSwitch += () =>
                {
                    var buttonText = ModListMenuButton?.GetLabelText();
                    if (buttonText is not null) buttonText.text = MenuButtonName;
                };
            }
        }
    }
    private readonly Lazy<I18n> _i18n;
    [Obsolete("Please override LanguagesEx instead of Languages")]
    protected virtual (Language.LanguageCode, string)[]? Languages => null;
    protected virtual List<(SupportedLanguages, string)>? LanguagesEx => null;
    protected virtual SupportedLanguages DefaultLanguageCode => SupportedLanguages.EN;
    public virtual string GetViewName() => GetName();
    public virtual bool FullScreen => false;

    public virtual void OnDebugDraw()
    {
        GUILayout.Label("Empty DebugView");
    }
}
public abstract class ModBase<T> : ModBase where T : ModBase<T>
{
    private static bool isTryLoad = false;
    private static void PreloadModBeforeModLoader()
    {
        isTryLoad = true;
        var type = typeof(T);
        try
        {
            ConstructorInfo constructor = type.GetConstructor(new Type[0]);
            if ((constructor?.Invoke(new object[0])) is Mod mod)
            {
                ModManager.skipMods.Add(typeof(T));
                ModLoaderHelper.AddModInstance(type, new()
                {
                    Mod = mod,
                    Enabled = false,
                    Error = null,
                    Name = mod.GetName()
                });
                //ModLoaderHelper.AddModInstance(type, mod, false, null, mod.GetName());
            }
        }
        catch (Exception e)
        {
            HKToolMod.logger.LogError(e);
            ModLoaderHelper.AddModInstance(type, new()
            {
                Mod = null,
                Enabled = false,
                Error = ModErrorState.Construct,
                Name = type.Name
            });
            //ModLoaderHelper.AddModInstance(type, null, false, "Construct", type.Name);
        }
    }
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindMod(typeof(T)) as T;
                if (_instance == null)
                {
                    if (!typeof(T).IsDefined(typeof(ModAllowEarlyInitializationAttribute)) || isTryLoad)
                    {
                        throw new InvalidOperationException("HKTool.Error.GetModInstaceBeforeLoad".GetFormat(typeof(T).Name));
                    }
                    PreloadModBeforeModLoader();
                }
            }
            if (_instance is null) throw new InvalidOperationException("HKTool.Error.GetModInstaceBeforeLoad".GetFormat(typeof(T).Name));
            return _instance;
        }
    }
    private static T? _instance = null;
    public ModBase(string? name = null) : base(name)
    {
        _instance = (T)this;
    }
}
public abstract class ModBaseWithSettings<TGlobalSettings, TLocalSettings> : ModBase where TGlobalSettings : new()
        where TLocalSettings : new()
{
    public virtual TLocalSettings localSettings { get; protected set; } = new();
    public TLocalSettings OnSaveLocal() => localSettings;
    public void OnLoadLocal(TLocalSettings s) => localSettings = s;
    public virtual TGlobalSettings globalSettings { get; protected set; } = new();
    public TGlobalSettings OnSaveGlobal() => globalSettings;
    public void OnLoadGlobal(TGlobalSettings s) => globalSettings = s;
}
public abstract class ModBaseWithSettings<T, TGlobalSettings, TLocalSettings> : ModBase<T> where TGlobalSettings : new()
        where TLocalSettings : new() where T : ModBaseWithSettings<T, TGlobalSettings, TLocalSettings>
{
    public virtual TLocalSettings localSettings { get; protected set; } = new();
    public TLocalSettings OnSaveLocal() => localSettings;
    public void OnLoadLocal(TLocalSettings s) => localSettings = s;
    public virtual TGlobalSettings globalSettings { get; protected set; } = new();
    public TGlobalSettings OnSaveGlobal() => globalSettings;
    public void OnLoadGlobal(TGlobalSettings s) => globalSettings = s;
}

