using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs; // Pour interagir avec Azure Blob Storage
using Microsoft.Azure.Functions.Worker; // Pour définir la fonction Azure
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus; // Pour le déclencheur Service Bus
using Microsoft.Extensions.Logging; // Pour le logging

namespace Company.Functions
{
    public class ServiceBusQueueFunction
    {
        // Constantes pour les noms des conteneurs source et destination
        private const string SourceContainerName = "images";
        private const string DestinationContainerName = "processed-images";

        [Function("ServiceBusQueueFunction")] // Attribut pour indiquer qu'il s'agit d'une fonction Azure
        public async Task Run(
            [ServiceBusTrigger("messagequeue", Connection = "ServiceBusConnectionString")] string blobName, // Déclencheur Service Bus
            FunctionContext context) // Contexte de la fonction pour le logging et autres
        {
            // Création d'un logger pour enregistrer les informations
            var logger = context.GetLogger("ServiceBusQueueFunction");
            logger.LogInformation($"Message reçu de la queue : {blobName}");

            // Récupération de la chaîne de connexion Azure Blob Storage
            var blobConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var blobServiceClient = new BlobServiceClient(blobConnectionString);

            // Accéder aux conteneurs source et destination
            var sourceContainer = blobServiceClient.GetBlobContainerClient(SourceContainerName);
            var destinationContainer = blobServiceClient.GetBlobContainerClient(DestinationContainerName);

            try
            {
                // Obtenir le blob source
                var sourceBlob = sourceContainer.GetBlobClient(blobName);
                if (!await sourceBlob.ExistsAsync()) // Vérification de l'existence du blob
                {
                    logger.LogError($"Le blob {blobName} n'existe pas.");
                    return;
                }

                // Télécharger le blob source dans un flux en mémoire
                await using var originalBlobStream = new MemoryStream();
                await sourceBlob.DownloadToAsync(originalBlobStream);

                // Traiter l'image et appliquer un watermark
                await using var processedBlobStream = new MemoryStream();
                ProcessImage(originalBlobStream, processedBlobStream, "Watermark Text");
                processedBlobStream.Position = 0; // Réinitialiser la position du flux

                // Charger le blob traité dans le conteneur de destination
                var destinationBlob = destinationContainer.GetBlobClient(blobName);
                await destinationBlob.UploadAsync(processedBlobStream, overwrite: true);

                logger.LogInformation($"Fichier {blobName} traité et sauvegardé dans {DestinationContainerName}.");

                // Supprimer le blob source après traitement
                await sourceBlob.DeleteAsync();
                logger.LogInformation($"Fichier original {blobName} supprimé.");
            }
            catch (Exception ex)
            {
                // Gestion des erreurs avec enregistrement des logs
                logger.LogError($"Erreur lors du traitement du fichier {blobName} : {ex.Message}");
            }
        }

        // Méthode pour traiter une image et ajouter un watermark
        private static void ProcessImage(Stream inputStream, Stream outputStream, string watermarkText)
        {
            using var image = Image.FromStream(inputStream); // Charger l'image depuis le flux
            using var bitmap = new Bitmap(image); // Créer un Bitmap modifiable
            using var graphics = Graphics.FromImage(bitmap); // Obtenir l'objet Graphics pour dessiner

            // Définir les paramètres de texte (font, couleur, style)
            var font = new Font("Arial", 24, FontStyle.Bold);
            var brush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)); // Couleur semi-transparente
            var textSize = graphics.MeasureString(watermarkText, font); // Mesurer la taille du texte

            // Ajouter le watermark en répétant le texte sur toute l'image
            for (float y = 0; y < bitmap.Height; y += textSize.Height + 20)
            {
                for (float x = 0; x < bitmap.Width; x += textSize.Width + 20)
                {
                    graphics.DrawString(watermarkText, font, brush, new PointF(x, y));
                }
            }

            // Sauvegarder l'image modifiée dans le flux de sortie
            bitmap.Save(outputStream, ImageFormat.Jpeg);
        }
    }
}



