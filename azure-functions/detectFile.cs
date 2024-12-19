using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker; // Pour définir une fonction Azure et son contexte
using Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs; // Pour le déclencheur BlobTrigger
using Microsoft.Extensions.Logging; // Pour la journalisation
using Azure.Messaging.ServiceBus; // Pour interagir avec Azure Service Bus

namespace Company.Functions
{
    public class BlobTriggeredFunction
    {
        private const string QueueName = "messagequeue"; // Nom de la queue dans Service Bus

        [Function("BlobTriggeredFunction")]
        public async Task Run(
            [BlobTrigger("images/{name}", Connection = "AzureWebJobsStorage")] Stream blob, // Déclencheur lié au container "images"
            string name, // Nom du blob (fichier)
            FunctionContext context) // Contexte de la fonction
        {
            // Récupération du logger pour écrire des logs
            var logger = context.GetLogger("BlobTriggeredFunction");
            logger.LogInformation($"Blob triggered: {name}, Size: {blob.Length} bytes");

            // Récupération de la chaîne de connexion à Azure Service Bus
            var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");

            // Création d'un client Service Bus
            await using var client = new ServiceBusClient(serviceBusConnectionString);
            var sender = client.CreateSender(QueueName); // Création d'un sender pour la queue spécifiée

            try
            {
                // Création d'un message avec le nom du blob
                var message = new ServiceBusMessage(name);

                // Envoi du message dans la queue
                await sender.SendMessageAsync(message);
                logger.LogInformation($"Message envoyé à la queue pour le fichier {name}");
            }
            catch (Exception ex)
            {
                // Gestion des erreurs et log de l'exception
                logger.LogError($"Erreur lors de l'envoi du message : {ex.Message}");
            }
            finally
            {
                // Libération des ressources : fermeture du sender et du client
                await sender.DisposeAsync();
                await client.DisposeAsync();
            }
        }
    }
}
