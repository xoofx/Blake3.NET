using System;

namespace Blake3
{
    public unsafe struct Hash
    {
        private const int HashSize = 32;
        private fixed byte _data[HashSize];

        public override string ToString()
        {
            return string.Create(HashSize * 2, this, (span, hash) =>
            {
                for (int i = 0; i < HashSize; i++)
                {
                    var b = hash._data[i];
                    span[i * 2] = Hex[(b >> 4) & 0xF];
                    span[i * 2 + 1] = Hex[b & 0xF];
                }
            });
        }

        private static ReadOnlySpan<char> Hex => new ReadOnlySpan<char>(new char[]
        {
            '0',
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9',
            'a',
            'b',
            'c',
            'd',
            'e',
            'f',
        });
    }
}