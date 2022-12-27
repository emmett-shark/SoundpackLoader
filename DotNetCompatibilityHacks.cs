// Allows usage of CallerArgumentExpression on older .net versions
// https://stackoverflow.com/a/70034587

namespace System.Runtime.CompilerServices;

#if !NET6_0_OR_GREATER

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    public CallerArgumentExpressionAttribute(string parameterName)
    {
        ParameterName = parameterName;
    }

    public string ParameterName { get; }
}

// Similar issue - https://stackoverflow.com/a/64749403
internal static class IsExternalInit { }

#endif
