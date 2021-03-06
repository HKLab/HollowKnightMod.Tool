
namespace HKTool;

public static class ModResManager
{
    private static Dictionary<Assembly, byte[]> resFileData = new();
    internal static void Init()
    {
        HKToolMod.logger.Log("Init ModResManager");
        On.System.Reflection.Assembly.GetManifestResourceStream_string += (orig, self, name) =>
        {
            var modResList = self.GetCustomAttribute<ModResourcesListAttribute>();
            var isCompression = self.IsDefined(typeof(EmbeddedResourceCompressionAttribute));
            if (modResList == null)
            {
                var stream = orig(self, name);
                if (isCompression && stream is not null)
                {
                    using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        var ms = new MemoryStream();
                        gzip.CopyTo(ms);
                        ms.Position = 0;
                        return ms;
                    }
                }
                return stream;
            }
            var resFile = resFileData.TryGetOrAddValue(self, () =>
            {
                var resPath = Path.Combine(Path.GetDirectoryName(self.Location), self.GetName().Name + ".modres");
                using (Stream rstream = File.OpenRead(resPath))
                {
                    if (isCompression)
                    {
                        using (var gzip = new GZipStream(rstream, CompressionMode.Decompress))
                        {
                            var ms = new MemoryStream();
                            gzip.CopyTo(ms);
                            ms.Position = 0;
                            return ms.ToArray();
                        }
                    }
                    else
                    {
                        var bytes = new byte[rstream.Length];
                        rstream.Read(bytes, 0, bytes.Length);
                        return bytes;
                    }
                }
            });
            var id = Array.FindIndex(modResList.names, x => x == name);
            if (id == -1) return null;
            var offset = modResList.offset[id];
            var st = new MemoryStream(resFile, offset, modResList.size[id], false);
            if (modResList.compress?[id] ?? false)
            {
                using (var gzip = new GZipStream(st, CompressionMode.Decompress))
                {
                    Debug.Log($"Decompress: {name}");
                    var ms = new MemoryStream();
                    gzip.CopyTo(ms);
                    st.Close();
                    Debug.Log($"Decompress finsihed: {ms.Length}");
                    ms.Position = 0;
                    return ms;
                }
            }
            return st;
        };
    }
    public static byte[]? GetManifestResourceBytes(this Assembly ass, string name)
    {
        using (var s = ass.GetManifestResourceStream(name))
        {
            if (s is null) return null;
            byte[] result;

            if (s is MemoryStream ms)
            {
                result = ms.ToArray();
            }
            else
            {
                result = new byte[s.Length];
                s.Read(result, 0, result.Length);
            }
            return result;
        }
    }
}
