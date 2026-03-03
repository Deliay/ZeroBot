using QRCoder;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Extension;

public static class LoginQrCodeExtension
{
    extension(LoginQrCodeResponse codeResponse)
    {
        public byte[] ToPngByteArray(string text)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(new PayloadGenerator.Url(text), QRCodeGenerator.ECCLevel.Q);
            var pngEncoder = new PngByteQRCode(data);
            return pngEncoder.GetGraphic(20);
        }
    }
}
