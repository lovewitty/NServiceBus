namespace NServiceBus
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Janitor;

    static class TextUtils
    {
        /// <summary>
        /// Acts like <see cref="string.TrimEnd" /> updating <paramref name="charCount" /> instead of allocating a string.
        /// </summary>
        public static unsafe void TrimEnd(char* chars, ref int charCount, char toTrim)
        {
            var end = charCount - 1;
            for (; end >= 0; --end)
            {
                var ch = chars[end];
                if (ch != toTrim)
                {
                    charCount = end + 1;
                    return;
                }
            }

            charCount = 0;
        }

        [SkipWeaving]
        public sealed unsafe class UnsafeReadBlockTextReader : TextReader
        {
            static UnsafeReadBlockTextReader()
            {
                wstrcpy = (wstrcpyDelegate) Delegate.CreateDelegate(typeof(wstrcpyDelegate), typeof(string).GetMethod("wstrcpy", BindingFlags.Static | BindingFlags.NonPublic));
            }

            public UnsafeReadBlockTextReader(char* ch, int length)
            {
                if (ch == null)
                {
                    throw new ArgumentNullException("s");
                }
                this.ch = ch;
                this.length = length;
            }

            /// <summary>Closes the <see cref="T:System.IO.StringReader" />.</summary>
            /// <filterpriority>2</filterpriority>
            public override void Close()
            {
                Dispose(true);
            }

            protected override void Dispose(bool disposing)
            {
                ch = null;
                pos = 0;
                length = 0;
                base.Dispose(disposing);
            }

            public override int Peek()
            {
                throw new NotImplementedException();
            }

            public override int Read()
            {
                throw new NotImplementedException();
            }

            public override int Read(char[] buffer, int index, int count)
            {
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer), "ArgumentNull_Buffer");
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index), "ArgumentOutOfRange_NeedNonNegNum");
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count), "ArgumentOutOfRange_NeedNonNegNum");
                if (buffer.Length - index < count)
                    throw new ArgumentException("Argument_InvalidOffLen");
                if (ch == null)
                {
                    throw new ObjectDisposedException(nameof(UnsafeReadBlockTextReader));
                }
                var count1 = length - pos;
                if (count1 > 0)
                {
                    if (count1 > count)
                        count1 = count;

                    fixed(char* chPtr2 = buffer)
                    {
                        wstrcpy(chPtr2 + index, ch + pos, count1);
                    }

                    pos += count1;
                }
                return count1;
            }

            public override string ReadToEnd()
            {
                throw new NotImplementedException();
            }

            public override string ReadLine()
            {
                throw new NotImplementedException();
            }

            public override Task<string> ReadLineAsync()
            {
                throw new NotImplementedException();
            }

            public override Task<string> ReadToEndAsync()
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadAsync(char[] buffer, int index, int count)
            {
                throw new NotImplementedException();
            }

            char* ch;
            int pos;
            int length;
            static readonly wstrcpyDelegate wstrcpy;

            delegate void wstrcpyDelegate(char* dmem, char* smem, int charCount);
        }
    }
}