namespace Ridl.Bmp
{
    internal interface IBmpHeaderV2OrAbove : IBmpHeader
    {
        uint RedMask { get; }
        uint GreenMask { get; }
        uint BlueMask { get; }
    }
}
