namespace Ridl.Bmp
{
    internal interface IBmpHeaderV4OrAbove : IBmpHeaderV3OrAbove
    {
        BmpColorSpace ColorSpace { get; }
    }
}
