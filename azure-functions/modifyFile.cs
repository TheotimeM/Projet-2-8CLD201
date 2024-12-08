using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public static class ServiceBusQueueFunction
{
    // à modifier par les bons noms
    private const string BlobConnectionString = "<YOUR_BLOB_STORAGE_CONNECTION_STRING>";
    private const string SourceContainerName = "images";
    private const string DestinationContainerName = "processed-images";

    [Function("ServiceBusQueueFunction")]
    public static void Run(
        [ServiceBusTrigger("imagequeue", Connection = "ServiceBusConnection")] string blobName,
        ILogger log)
    {
        log.LogInformation($"Message reçu de la queue : {blobName}");

        try
        {
            // Accéder au fichier blob
            BlobServiceClient blobServiceClient = new(BlobConnectionString);
            BlobContainerClient sourceContainer = blobServiceClient.GetBlobContainerClient(SourceContainerName);
            BlobClient sourceBlob = sourceContainer.GetBlobClient(blobName);

            if (!sourceBlob.Exists())
            {
                log.LogError($"Le blob {blobName} n'existe pas.");
                return;
            }

            // Télécharger le blob dans un Stream
            using Stream originalBlobStream = sourceBlob.OpenRead();
            using MemoryStream processedBlobStream = new();

            // Appliquer le traitement (ajout du watermark)
            ProcessImage(originalBlobStream, processedBlobStream, "Watermark Text");
            processedBlobStream.Position = 0;

            // Sauvegarder le fichier traité dans un autre conteneur
            BlobContainerClient destinationContainer = blobServiceClient.GetBlobContainerClient(DestinationContainerName);
            BlobClient destinationBlob = destinationContainer.GetBlobClient(blobName);
            destinationBlob.Upload(processedBlobStream, overwrite: true);
            log.LogInformation($"Fichier {blobName} traité et sauvegardé dans {DestinationContainerName}.");

            // Supprimer l'original
            sourceBlob.Delete();
            log.LogInformation($"Fichier original {blobName} supprimé.");
        }
        catch (Exception ex)
        {
            log.LogError($"Erreur lors du traitement du fichier {blobName} : {ex.Message}");
        }
    }

    private static void ProcessImage(Stream inputStream, Stream outputStream, string watermarkText)
    {
        using Image image = Image.FromStream(inputStream);
        using Bitmap bitmap = new(image);
        using Graphics graphics = Graphics.FromImage(bitmap);

        // Définir les paramètres du watermark
        Font font = new("Arial", 24, FontStyle.Bold);
        SolidBrush brush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)); // Transparence ajustée
        SizeF textSize = graphics.MeasureString(watermarkText, font);

        // Répéter le watermark sur toute l'image
        for (float y = 0; y < bitmap.Height; y += textSize.Height + 20)
        {
            for (float x = 0; x < bitmap.Width; x += textSize.Width + 20)
            {
                graphics.DrawString(watermarkText, font, brush, new PointF(x, y));
            }
        }

        // Sauvegarder l'image avec le watermark
        bitmap.Save(outputStream, ImageFormat.Jpeg);
    }
}
