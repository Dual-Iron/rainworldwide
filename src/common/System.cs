#pragma warning disable CS9113 // Parameter is unread.

namespace System
{
    readonly struct Index(int value, bool fromEnd = false)
    {
        public int GetOffset(int length) => fromEnd ? value + length + 1 : value;
        public static implicit operator Index(int value) => new(value);
    }
    readonly struct Range(Index start, Index end)
    {
        public Index Start { get; } = start;
        public Index End { get; } = end;
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter)]
    class CallerArgumentExpressionAttribute(string expression) : Attribute
    {
    }
}
