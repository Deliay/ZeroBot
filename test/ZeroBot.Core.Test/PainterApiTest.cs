using ZeroBot.Painter;

namespace ZeroBot.Core.Test;

public class PainterApiTest
{
    [Fact]
    public void DetectImageType_ShouldRecognizePng()
    {
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(("image/png", "png"), PainterApi.DetectImageType(data));
    }

    [Fact]
    public void DetectImageType_ShouldRecognizeJpeg()
    {
        var data = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        Assert.Equal(("image/jpeg", "jpg"), PainterApi.DetectImageType(data));
    }

    [Fact]
    public void DetectImageType_ShouldRecognizeGif()
    {
        var data = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
        Assert.Equal(("image/gif", "gif"), PainterApi.DetectImageType(data));
    }

    [Fact]
    public void DetectImageType_ShouldRecognizeWebp()
    {
        var data = new byte[16];
        data[0] = 0x52; data[1] = 0x49; data[2] = 0x46; data[3] = 0x46;
        data[8] = 0x57; data[9] = 0x45; data[10] = 0x42; data[11] = 0x50;
        Assert.Equal(("image/webp", "webp"), PainterApi.DetectImageType(data));
    }

    [Fact]
    public void DetectImageType_ShouldFallbackForUnknown()
    {
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.Equal(("application/octet-stream", "bin"), PainterApi.DetectImageType(data));
    }
}
