namespace Yamadev.YamaStream
{
    // A ColorDefinition's role. Prefabs save the number, so a new role takes
    // the next one and an old one keeps its number.
    public enum ColorType
    {
        Primary = 0,
        Secondary = 1,
        // Worked out from the colour set's primary colour (issue #161).
        PrimaryContainer = 2,
        OnPrimary = 3,
        // The ColorPalette's neutral colours.
        Background = 4,
        Surface = 5,
        SurfaceVariant = 6,
        OnSurface = 7,
        OnSurfaceVariant = 8
    }
}
