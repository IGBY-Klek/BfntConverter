using SixLabors.ImageSharp;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the bfnt and pi formats.
    /// </summary>
    public sealed class BfntConfigurationModule : IConfigurationModule
    {
        public void Configure(Configuration configuration)
        {
            //configuration.ImageFormatsManager.SetEncoder(BfntFormat.Instance, new PngEncoder());
            configuration.ImageFormatsManager.SetDecoder(BfntFormat.Instance, new BfntDecoder());
            configuration.ImageFormatsManager.SetDecoder(PiFormat.Instance, new PiDecoder());
            configuration.ImageFormatsManager.SetDecoder(MpnFormat.Instance, new MpnDecoder());
            configuration.ImageFormatsManager.AddImageFormatDetector(new BfntImageFormatDetector());
            configuration.ImageFormatsManager.AddImageFormatDetector(new PiImageFormatDetector());
            configuration.ImageFormatsManager.AddImageFormatDetector(new MpnImageFormatDetector());
        }
    }
}
