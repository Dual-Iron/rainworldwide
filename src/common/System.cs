#pragma warning disable CS9113 // Parameter is unread.

using System.Collections;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter)]
    class CallerArgumentExpressionAttribute(string expression) : Attribute
    {
    }
}
