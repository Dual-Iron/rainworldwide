#pragma warning disable CS9113 // Parameter is unread.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter)]
    class CallerArgumentExpressionAttribute(string expression) : Attribute
    {
    }
}
