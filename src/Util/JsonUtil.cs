using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundpackLoader;

internal class JsonUtil
{
    public static T? ReadFile<T>(FileInfo file) where T: class
    {
        var settings = new JsonSerializerSettings();
        var ser = new JsonSerializer();
        using (var r = new StreamReader(file.FullName))
        using (var jReader = new JsonTextReader(r))
        {
            try
            {
                return ser.Deserialize<T>(jReader);
            }
            catch (Exception ex)
            {
                DebugUtil.Dump(ex, LogLevel.Warning);
                return null;
            }
        }
    }

    public static T? ReadFile<T>(string path) where T : class
    {
        return ReadFile<T>(new FileInfo(path));
    }
}
