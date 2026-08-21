namespace Yamadev.YamaStream
{
  // Provider info carried in the track extension (see issue #72).
  // Format v1: byte[4] = [0x4B ('K'), 0x50 ('P'), version, provider].
  // Anything that is not exactly 4 bytes with matching magic and version
  // is treated as "provider unknown" so unrelated or future extension
  // payloads are ignored safely.
  public static class TrackProviderUtils
  {
    public const byte ProviderUnknown = 0x00;
    public const byte ProviderYouTube = 0x01;

    private const byte MagicByte0 = 0x4B;
    private const byte MagicByte1 = 0x50;
    private const byte Version = 0x01;

    public static byte[] BuildProviderExtension(byte provider)
    {
      return new byte[] { MagicByte0, MagicByte1, Version, provider };
    }

    public static byte GetProvider(object[] track)
    {
      byte[] extension = TrackUtils.GetExtension(track);
      if (extension.Length != 4) return ProviderUnknown;
      if (extension[0] != MagicByte0 || extension[1] != MagicByte1) return ProviderUnknown;
      if (extension[2] != Version) return ProviderUnknown;
      // Values not defined in v1 normalize to ProviderUnknown so callers
      // never observe undefined provider ids.
      return extension[3] == ProviderYouTube ? ProviderYouTube : ProviderUnknown;
    }

    public static bool IsYouTube(object[] track) => GetProvider(track) == ProviderYouTube;
  }
}
