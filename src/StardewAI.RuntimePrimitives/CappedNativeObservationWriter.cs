using System;
using System.IO;
using System.Text;

namespace StardewAI.RuntimePrimitives
{
    public sealed class CappedNativeObservationWriter : TextWriter
    {
        private readonly int maximumCharacters;
        private readonly StringBuilder contents = new StringBuilder();
        public CappedNativeObservationWriter(int maximumCharacters)
        {
            if (maximumCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            this.maximumCharacters = maximumCharacters;
        }
        public override Encoding Encoding => Encoding.UTF8;
        public bool LimitExceeded { get; private set; }
        private void Check(int count)
        {
            if (count > maximumCharacters - contents.Length)
            {
                LimitExceeded = true;
                throw new InvalidDataException("Native observation payload exceeds its character budget.");
            }
        }
        public override void Write(char value) { Check(1); contents.Append(value); }
        public override void Write(string? value) { Check(value?.Length ?? 0); contents.Append(value); }
        public override void Write(char[] buffer, int index, int count) { Check(count); contents.Append(buffer, index, count); }
        public override void Write(ReadOnlySpan<char> buffer) { Check(buffer.Length); contents.Append(buffer); }
        public override string ToString() => contents.ToString();
    }
}
