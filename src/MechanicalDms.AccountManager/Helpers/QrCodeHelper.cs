using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using KaiheilaBot.Core.Models.Requests.Media;
using KaiheilaBot.Core.Services.IServices;
using QRCoder;

namespace MechanicalDms.AccountManager.Helpers
{
    public static class QrCodeHelper
    {
        public static async Task<string> GenerateAndUploadQrCode(string url, IHttpApiRequestService api)
        {
            var generator = new PayloadGenerator.Url(url);
            var payload = generator.ToString();

            var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.L);
            var qrCode = new PngByteQRCode(qrData);
            var qrCodePngBytes = qrCode.GetGraphic(64);

            var filePath = Path.Combine(Configuration.PluginPath, "QrCodeCache", Guid.NewGuid() + ".png");
            await File.WriteAllBytesAsync(filePath, qrCodePngBytes);
            await Task.Delay(100);
            
            var response = await api.GetResponse(new CreateAssetRequest()
            {
                FilePath = filePath
            });

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var json = JsonDocument.Parse(response.Content).RootElement;
            var imageUrl = json.GetProperty("data").GetProperty("url").GetString();
            
            File.Delete(filePath);
            
            return imageUrl;
        }
    }
}