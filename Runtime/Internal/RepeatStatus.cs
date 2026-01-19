using System;
using UdonSharp;

namespace Yamadev.YamaStream
{
  public class RepeatStatus : UdonSharpBehaviour
  {
    public static RepeatStatus New(ulong packedData)
    {
      object[] arr = new object[3];

      arr[0] = (packedData & (1ul << 63)) != 0;
      ulong clearedPacked = packedData & ~(1ul << 63);
      uint startBits = (uint)((clearedPacked >> 32) & 0xFFFFFFFF);
      uint endBits = (uint)(clearedPacked & 0xFFFFFFFF);

      arr[1] = BitConverter.ToSingle(BitConverter.GetBytes(startBits), 0);
      arr[2] = BitConverter.ToSingle(BitConverter.GetBytes(endBits), 0);

      return (RepeatStatus)(object)arr;
    }

    public static RepeatStatus New(bool flag, float start, float end)
    {
      object[] arr = new object[] { flag, start, end };
      return (RepeatStatus)(object)arr;
    }
  }

  public static class RepeatStatusExtensions
  {
    public static ulong GetPackedData(this RepeatStatus obj)
    {
      ulong flagBit = ((bool)((object[])(object)obj)[0] ? 1ul : 0ul) << 63;
      uint startBits = BitConverter.ToUInt32(BitConverter.GetBytes((float)((object[])(object)obj)[1]), 0);
      uint endBits = BitConverter.ToUInt32(BitConverter.GetBytes((float)((object[])(object)obj)[2]), 0);
      return flagBit | ((ulong)startBits << 32) | endBits;
    }

    public static float GetStartTime(this RepeatStatus obj)
    {
      return (float)((object[])(object)obj)[1];
    }

    public static float GetEndTime(this RepeatStatus obj)
    {
      return (float)((object[])(object)obj)[2];
    }

    public static void SetStartTime(this RepeatStatus obj, float startTime)
    {
      ((object[])(object)obj)[1] = startTime;
    }

    public static void SetEndTime(this RepeatStatus obj, float endTime)
    {
      ((object[])(object)obj)[2] = endTime;
    }

    public static bool IsOn(this RepeatStatus obj)
    {
      return (bool)((object[])(object)obj)[0];
    }

    public static void TurnOn(this RepeatStatus obj)
    {
      ((object[])(object)obj)[0] = true;
    }

    public static void TurnOff(this RepeatStatus obj)
    {
      ((object[])(object)obj)[0] = false;
    }
  }
}
