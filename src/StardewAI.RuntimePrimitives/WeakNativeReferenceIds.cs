using System;
using System.Runtime.CompilerServices;

namespace StardewAI.RuntimePrimitives
{
    public sealed class WeakNativeReferenceIds
    {
        private sealed class Identity
        {
            internal readonly string Value = Guid.NewGuid().ToString("N");
        }
        private readonly ConditionalWeakTable<object, Identity> values = new ConditionalWeakTable<object, Identity>();
        public string For(object value) => values.GetValue(value, _ => new Identity()).Value;
    }
}
