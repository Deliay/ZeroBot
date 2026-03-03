using System.Text;
using Net.Codecrete.QrCodeGenerator;

namespace ZeroBot.Endfield.Playground;

public static class QrCodeExtensions
{
    extension(QrCode)
    {
        public static string EnhancedEncodeText(string payload, bool compatible = false)
        {
            var qrcode = QrCode.EncodeText(payload, QrCode.Ecc.Low);

            StringBuilder result = new();
            for (var y = 0; y < qrcode.Size; y += 2)
            {
                for (var x = 0; x < qrcode.Size; x++)
                {
                    bool top = qrcode.GetModule(x, y);
                    bool bottom = qrcode.GetModule(x, y + 1);

                    result.Append((top, bottom) switch
                    {
                        (true, true) => compatible ? '@' : '█',
                        (true, false) => compatible ? '^' : '▀',
                        (false, true) => compatible ? '.' : '▄',
                        (false, false) => ' ',
                    });
                }
                if (y < qrcode.Size) result.Append('\n');
            }

            return result.ToString();
        }
    }
}