using Cave;
using System;
using System.Text;

namespace Test
{
    public struct SettingsStructFields
    {
        public string SampleString;
        public sbyte SampleInt8;
        public short SampleInt16;
        public int SampleInt32;
        public long SampleInt64;
        public byte SampleUInt8;
        public ushort SampleUInt16;
        public uint SampleUInt32;
        public ulong SampleUInt64;
        public float SampleFloat;
        public double SampleDouble;
        public decimal SampleDecimal;
        public bool SampleBool;
        public DateTime SampleDateTime;
        public TimeSpan SampleTimeSpan;
        public SettingEnum SampleEnum;
        public SettingFlagEnum SampleFlagEnum;
        public uint? SampleNullableUInt32;

        static readonly Random random = new Random(Environment.TickCount);

        public static SettingsStructFields Random()
        {
            var len = random.Next(0, 128);
            char[] str;
            if (random.Next(1, 100) < 51)
            {
                byte[] buf = new byte[len * 2];
                random.NextBytes(buf);
                str = Encoding.Unicode.GetString(buf).ToCharArray();
            }
            else
            {
                byte[] buf = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    if (buf[i] < 16) buf[i] = (byte)' ';
                }
                random.NextBytes(buf);
                str = Encoding.GetEncoding(1250 + random.Next(0, 5)).GetChars(buf);
            }
            
            // replace characters that cannot be saved to ini file
            for(int i = 0; i< str.Length; i++)
            {
                if (str[i] < 32) str[i] = '_';
            }

            var dateTime = DateTime.Today.AddSeconds(random.Next(1, 60 * 60 * 24));
            return new SettingsStructFields()
            {
                SampleString = new string(str),
                SampleBool = random.Next(1, 100) < 51,
                SampleDateTime = dateTime,
                SampleTimeSpan = TimeSpan.FromSeconds(random.NextDouble()),
                SampleDecimal = (decimal)random.NextDouble() / (decimal)random.NextDouble(),
                SampleDouble = random.NextDouble(),
                SampleEnum = (SettingEnum)random.Next(0, 9),
                SampleFlagEnum = (SettingFlagEnum)random.Next(0, 1 << 10),
                SampleFloat = (float)random.NextDouble(),
                SampleInt16 = (short)random.Next(short.MinValue, short.MaxValue),
                SampleInt32 = random.Next() - random.Next(),
                SampleInt64 = ((long)random.Next() - (long)random.Next()) * (long)random.Next(),
                SampleInt8 = (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue),
                SampleUInt8 = (byte)random.Next(byte.MinValue, byte.MaxValue),
                SampleUInt16 = (ushort)random.Next(ushort.MinValue, ushort.MaxValue),
                SampleUInt32 = (uint)random.Next() + (uint)random.Next(),
                SampleUInt64 = ((ulong)random.Next() + (ulong)random.Next()) * (ulong)random.Next(),
                SampleNullableUInt32 = random.Next(1, 100) < 20 ? null : (uint?)random.Next(),
            };
        }
    }
}
