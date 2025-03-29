namespace Ridl.Bmp
{
    internal interface IBmpHeaderV3OrAbove : IBmpHeaderV2OrAbove
    {
        uint AlphaMask { get; }
    }
}
