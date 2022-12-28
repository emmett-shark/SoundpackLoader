using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SoundpackLoader;

internal class DebugUtil
{
    public static void Dump(object? obj, LogLevel level = LogLevel.Info, [CallerArgumentExpression(nameof(obj))] string objExpression = "<unknown>")
    {
        var options = new JsonSerializerSettings();
        options.Formatting = Formatting.Indented;
        options.MaxDepth = 6;
        options.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

        var data = JsonConvert.SerializeObject(obj, options);
        Plugin.Logger.Log(level, $"{objExpression} = {data}");
    }
}
