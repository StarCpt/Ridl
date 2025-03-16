namespace SharpPng
{
    public enum PngPixelFormat : byte
    {
        /// <summary>
        /// Each pixel is a greyscale sample
        /// </summary>
        /// 
        /// <remarks>
        /// Allowed bit depths: 1, 2, 4, 8, 16
        /// </remarks>
        Grayscale = 0,

        /// <summary>
        /// Each pixel is an R,G,B triple
        /// </summary>
        /// 
        /// <remarks>
        /// Allowed bit depths: 8, 16
        /// </remarks>
        Rgb = 2,

        /// <summary>
        /// Each pixel is a palette index; a PLTE chunk shall appear.
        /// </summary>
        /// 
        /// <remarks>
        /// Allowed bit depths: 1, 2, 4, 8
        /// </remarks>
        Indexed = 3,

        /// <summary>
        /// Each pixel is a greyscale sample followed by an alpha sample.
        /// </summary>
        /// 
        /// <remarks>
        /// Allowed bit depths: 8, 16
        /// </remarks>
        GrayscaleWithAlpha = 4,

        /// <summary>
        /// Each pixel is an R,G,B triple followed by an alpha sample.
        /// </summary>
        /// 
        /// <remarks>
        /// Allowed bit depths: 8, 16
        /// </remarks>
        Rgba = 6,
    }
}
