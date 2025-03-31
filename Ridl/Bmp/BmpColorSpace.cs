namespace Ridl.Bmp
{
    /// <summary>
    /// v4: <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/eb4bbd50-b3ce-4917-895c-be31f214797f"/>
    /// v5: <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/3c289fe1-c42e-42f6-b125-4b5fc49a2b20"/>
    /// </summary>
    internal enum BmpColorSpace : uint
    {
        LCS_CALIBRATED_RGB      = 0x00000000,
        LCS_sRGB                = 0x73524742,
        LCS_WINDOWS_COLOR_SPACE = 0x57696E20,
        LCS_PROFILE_LINKED      = 0x4C494E4B, // v5 header only
        LCS_PROFILE_EMBEDDED    = 0x4D424544, // v5 header only
    }
}
