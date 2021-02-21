using BigMission.DeviceApp.Shared;
using System;
using System.Text;

namespace BigMission.CanTools
{
    /// <summary>
    /// Functions for decoding and processing CAN messages.
    /// </summary>
    public class CanUtilities
    {
        public const uint MAX_11_BIT_ID = 0b111_1111_1111;
        public const uint MAX_29_BIT_ID = 0b1_1111_1111_1111_1111_1111_1111_1111;

        public static float Decode(ulong data, ChannelMappingDto map)
        {
            var value = ExtractValue(data, map);
            value = (float)RunFormula(value, map);
            value = (float)ChannelConversion.Convert(value, map.Conversion);
            return value;
        }

        private static float ExtractValue(ulong data, ChannelMappingDto map)
        {
            // Swap to default to Little Endian from source.  This will move the values 
            // opposite of what's represented visually in AIM but keep the offsets the same for bit shifting. 
            data = Swap(data);

            // Pull out the individual value
            ulong clearMask = ~0UL >> ((8 - map.Length) * 8);
            clearMask <<= map.Offset * 8;
            ulong maskedData = (data & clearMask) >> (map.Offset * 8);

            // Byte swap if needed
            if (!map.IsBigEndian)
            {
                maskedData = Swap(maskedData);
            }

            // Resolve Type
            var buff = BitConverter.GetBytes(maskedData);
            if (map.SourceType == ChannelSourceType.UNSIGNED)
            {
                return maskedData;
            }
            else if (map.SourceType == ChannelSourceType.SIGNED)
            {
                if (map.Length == 1)
                {
                    throw new NotImplementedException();
                }
                else if (map.Length == 2)
                {
                    return BitConverter.ToInt16(buff, 0);
                }
                else if (map.Length == 3)
                {
                    throw new NotImplementedException();
                }
                else if (map.Length == 4)
                {
                    return BitConverter.ToInt32(buff, 0);
                }
            }
            else if (map.SourceType == ChannelSourceType.FLOAT)
            {
                return BitConverter.ToSingle(buff, 0);
            }
            else if (map.SourceType == ChannelSourceType.SIGN_MAGNITUDE)
            {
                // Ref autosport code:
                // sign-magnitude is used in cases where there's a sign bit
                // and an absolute value indicating magnitude.
                // e.g. BMW E46 steering angle sensor
                uint sign = ((uint)1) << (map.Length - 1);
                return maskedData < sign ? maskedData : -(float)(maskedData & (sign - 1));
            }
            return 0.0f;
        }


        public static ulong Swap(ulong val)
        {
            return (val & 0x00000000000000FFUL) << 56 | (val & 0x000000000000FF00UL) << 40 |
                   (val & 0x0000000000FF0000UL) << 24 | (val & 0x00000000FF000000UL) << 8 |
                   (val & 0x000000FF00000000UL) >> 8 | (val & 0x0000FF0000000000UL) >> 24 |
                   (val & 0x00FF000000000000UL) >> 40 | (val & 0xFF00000000000000UL) >> 56;
        }

        public static double RunFormula(double value, ChannelMappingDto mapping)
        {
            value *= mapping.FormulaMultipler;
            if (mapping.FormulaDivider != 0)
                value /= mapping.FormulaDivider;
            value += mapping.FormulaConst;
            return value;
        }

        public static ulong Encode(ulong data, float value, ChannelMappingDto map)
        {
            value = (float)RunFormula(value, map);

            byte[] repBytes = null;
            if (map.SourceType == ChannelSourceType.UNSIGNED)
            {
                if (map.Length == 1)
                {
                    repBytes = BitConverter.GetBytes((byte)value);
                }
                else if (map.Length == 2)
                {
                    repBytes = BitConverter.GetBytes((ushort)value);
                }
                else if (map.Length == 3)
                {
                    repBytes = BitConverter.GetBytes((uint)value);
                }
                else if (map.Length == 4)
                {
                    repBytes = BitConverter.GetBytes((uint)value);
                }
            }
            else if (map.SourceType == ChannelSourceType.SIGNED)
            {
                if (map.Length == 1)
                {
                    repBytes = BitConverter.GetBytes((sbyte)value);
                }
                else if (map.Length == 2)
                {
                    repBytes = BitConverter.GetBytes((short)value);
                }
                else if (map.Length == 3)
                {
                    throw new NotImplementedException();
                }
                else if (map.Length == 4)
                {
                    repBytes = BitConverter.GetBytes((int)value);
                }
            }
            else if (map.SourceType == ChannelSourceType.FLOAT)
            {
                repBytes = BitConverter.GetBytes(value);
                if (map.Length != 4)
                {
                    throw new InvalidOperationException("Floating point represenation must be 4 bytes long.");
                }
            }
            else if (map.SourceType == ChannelSourceType.SIGN_MAGNITUDE)
            {
                throw new NotImplementedException();
            }

            // Pad size to ensure lenght of 8 for ToUInt64
            byte[] buff = new byte[8];
            for (int i = 0; i < repBytes.Length; i++)
            {
                buff[i] = repBytes[i];
            }

            ulong repLong = BitConverter.ToUInt64(buff);
            ulong clearMask = ~0UL >> ((8 - map.Length) * 8);
            clearMask <<= map.Offset * 8;
            data &= ~clearMask;
            data |= repLong << (map.Offset * 8);

            //// Swap to default to Little Endian from source.  This will move the values 
            //// opposite of what's represented visually in AIM but keep the offsets the same for bit shifting. 
            //data = Swap(data);

            return data;
        }

        public static string InferCanIdString(uint id)
        {
            if (id > MAX_11_BIT_ID)
            {
                return Get29BitCanId(id);
            }
            return Get29BitCanId(id);
        }

        public static string Get11BitCanId(uint id)
        {
            var s = string.Format("{0:X3}", id);
            return s;
        }

        public static string Get29BitCanId(uint id)
        {
            return string.Format("{0:X8}", id);
        }

        public static string GetDataString(ulong data)
        {
            return string.Format("{0:X16}", data);
        }

        /// <summary>
        /// Convert data to byte string of exact message's length.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string ConvertExactString(ulong data, int length)
        {
            var sbData = new StringBuilder();
            var bytes = BitConverter.GetBytes(data);
            for (int i = length - 1; i >= 0; i--)
            {
                sbData.Append(bytes[i].ToString("X2"));
            }
            return sbData.ToString();
        }

        public static CanSpeed ParseSpeed(string s)
        {
            return s switch
            {
                "1000000" => CanSpeed._1Mbit,
                "800000" => CanSpeed._800Kbit,
                "500000" => CanSpeed._500Kbit,
                "250000" => CanSpeed._250Kbit,
                "125000" => CanSpeed._125Kbit,
                "100000" => CanSpeed._100Kbit,
                "50000" => CanSpeed._50Kbit,
                "20000" => CanSpeed._20Kbit,
                "10000" => CanSpeed._10Kbit,
                _ => throw new NotSupportedException(),
            };
        }

        public static string ConvertSpeed(CanSpeed cs)
        {
            return cs switch
            {
                CanSpeed._1Mbit => "1000000",
                CanSpeed._800Kbit => "800000",
                CanSpeed._500Kbit => "500000",
                CanSpeed._250Kbit => "250000",
                CanSpeed._125Kbit => "125000",
                CanSpeed._100Kbit => "100000",
                CanSpeed._50Kbit => "50000",
                CanSpeed._20Kbit => "20000",
                CanSpeed._10Kbit => "10000",
                _ => throw new NotSupportedException(),
            };
        }
    }
}
