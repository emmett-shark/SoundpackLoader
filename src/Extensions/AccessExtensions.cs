using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SoundpackLoader;

internal static class AccessExtensions
{
    public static T CallStaticMethod<T>(this Type type, string methodName, params object[] args)
    {
        var m = type.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static
        );
        if (m == null)
            throw new MissingMemberException(type.Name, methodName);

        return (T) m.Invoke(null, args);
    }
}
