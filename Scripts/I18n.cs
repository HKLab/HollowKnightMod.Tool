﻿
namespace HKTool;

public class I18n
{
    public static List<I18n> Instances { get; private set; } = new List<I18n>();
    public readonly string modName;
    public readonly string modDir;
    public Dictionary<string, string> Current
    { get; private set; } = new Dictionary<string, string>();
    public Dictionary<LanguageCode, string> Languages { get; private set; } = new Dictionary<LanguageCode, string>();
    public I18n(string modName = "", string modDir = "")
    {
        this.modName = modName;
        this.modDir = modDir;
        Instances.Add(this);
    }
    public bool UseLanguageHook
    {
        set
        {
            Modding.ModHooks.LanguageGetHook -= ModHooks_LanguageGetHook;
            if (value)
            {
                Modding.ModHooks.LanguageGetHook += ModHooks_LanguageGetHook;
            }
        }
    }

    private string ModHooks_LanguageGetHook(string key, string sheetTitle, string orig)
    {
        if (TryGet(key, out var s))
        {
            return s;
        }
        return orig;
    }

    public bool ChangeLanguage(LanguageCode code)
    {
        if (Languages.TryGetValue(code, out var v))
        {
            Current.Clear();
            foreach (var l in v.Split('\n'))
            {
                var l2 = l;
                if (l2.StartsWith("#")) continue;
                int s = l2.IndexOf('=');
                if (s == -1) continue;
                var n = l2.Substring(0, s);
                var val = l2.Substring(s + 1).Replace("\\n", "\n");
                Current.Add(n, val);
                Modding.Logger.Log($"I18n: {n} = {val}");
            }
            return true;
        }
        return false;
    }
    public string Get(string key)
    {
        if (Current.TryGetValue(key, out var v)) return v;
        return key;
    }
    public bool TryGet(string key, out string val)
    {
        return Current.TryGetValue(key, out val);
    }

    public static string GlobalGet(string key)
    {
        foreach (var v in Instances)
        {
            if (v.TryGet(key, out var s))
            {
                return s;
            }
        }
        return key;
    }
    public void AddLanguage(LanguageCode code, string data)
    {
        Languages[code] = data;
    }
    public void AddLanguage(LanguageCode code, Stream stream, bool closeStream = true)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        byte[] b = new byte[stream.Length];
        stream.Read(b, 0, (int)stream.Length);
        if (closeStream) stream.Close();
        AddLanguage(code, Encoding.UTF8.GetString(b));
    }

    public void AddLanguage(LanguageCode src, LanguageCode dst)
    {
        if (Languages.TryGetValue(src, out var v))
        {
            Languages[dst] = v;
        }
    }
    public void UseGameLanguage(LanguageCode defaultCode = LanguageCode.EN, bool allowExternLang = false)
    {
        var current = Language.Language.CurrentLanguage();
        if (!ChangeLanguage(current))
        {
            if(allowExternLang)
            {
                var langName = $"{modName}.{current.ToString().ToLower()}.lang";
                var langPath = Path.Combine(modDir, langName);
                if(File.Exists(langPath))
                {
                    AddLanguage(current, File.ReadAllText(langPath));
                    ChangeLanguage(current);
                }
                else if(File.Exists(langName))
                {
                    AddLanguage(current, File.ReadAllText(langName));
                    ChangeLanguage(current);
                }
                else
                {
                    ChangeLanguage(defaultCode);
                }
                return;
            }
            ChangeLanguage(defaultCode);
        }
    }
}

