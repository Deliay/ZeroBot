using QRCoder;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Extension;

public static class LoginQrCodeExtension
{
    extension(LoginQrCodeResponse response)
    {
        public byte[] ToPngQrCodeByteArray()
        {   
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(response.scanUrl, QRCodeGenerator.ECCLevel.Q);
            var pngEncoder = new PngByteQRCode(data);
            return pngEncoder.GetGraphic(20);
        }
    }
}
