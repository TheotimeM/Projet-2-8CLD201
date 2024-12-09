using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker; // Pour [Function], FunctionContext
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus; // Pour [ServiceBusTrigger]
using Microsoft.Extensions.Logging;

namespace Company.Functions
{
    public class ServiceBusQueueFunction
    {
        private const string SourceContainerName = "images";
        private const string DestinationContainerName = "processed-images";

        [Function("ServiceBusQueueFunction")]
        public async Task Run(
            [ServiceBusTrigger("imagequeue", Connection = "ServiceBusConnectionString")] string blobName,
            FunctionContext context)
        {
            var logger = context.GetLogger("ServiceBusQueueFunction");
            logger.LogInformation($"Message reçu de la queue : {blobName}");

            var blobConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            var sourceContainer = blobServiceClient.GetBlobContainerClient(SourceContainerName);
            var destinationContainer = blobServiceClient.GetBlobContainerClient(DestinationContainerName);

            try
            {
                var sourceBlob = sourceContainer.GetBlobClient(blobName);
                if (!await sourceBlob.ExistsAsync())
                {
                    logger.LogError($"Le blob {blobName} n'existe pas.");
                    return;
                }

                await using var originalBlobStream = new MemoryStream();
                await sourceBlob.DownloadToAsync(originalBlobStream);

                await using var processedBlobStream = new MemoryStream();
                ProcessImage(originalBlobStream, processedBlobStream, "Watermark Text");
                processedBlobStream.Position = 0;

                var destinationBlob = destinationContainer.GetBlobClient(blobName);
                await destinationBlob.UploadAsync(processedBlobStream, overwrite: true);

                logger.LogInformation($"Fichier {blobName} traité et sauvegardé dans {DestinationContainerName}.");

                await sourceBlob.DeleteAsync();
                logger.LogInformation($"Fichier original {blobName} supprimé.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Erreur lors du traitement du fichier {blobName} : {ex.Message}");
            }
        }

        private static void ProcessImage(Stream inputStream, Stream outputStream, string watermarkText)
        {
            using var image = Image.FromStream(inputStream);
            using var bitmap = new Bitmap(image);
            using var graphics = Graphics.FromImage(bitmap);

            var font = new Font("Arial", 24, FontStyle.Bold);
            var brush = new SolidBrush(Color.FromArgb(50, 255, 255, 255));
            var textSize = graphics.MeasureString(watermarkText, font);

            for (float y = 0; y < bitmap.Height; y += textSize.Height + 20)
            {
                for (float x = 0; x < bitmap.Width; x += textSize.Width + 20)
                {
                    graphics.DrawString(watermarkText, font, brush, new PointF(x, y));
                }
            }

            bitmap.Save(outputStream, ImageFormat.Jpeg);
        }
    }
}
