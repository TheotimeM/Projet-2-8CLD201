using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker; // pour Function, FunctionContext
using Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs; // pour BlobTrigger
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;


namespace Company.Functions
{
    public class BlobTriggeredFunction
    {
        private const string QueueName = "imagequeue";

        [Function("BlobTriggeredFunction")]
        public async Task Run(
            [BlobTrigger("images/{name}", Connection = "AzureWebJobsStorage")] Stream blob,
            string name,
            FunctionContext context)
        {
            var logger = context.GetLogger("BlobTriggeredFunction");
            logger.LogInformation($"Blob triggered: {name}, Size: {blob.Length} bytes");

            var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            await using var client = new ServiceBusClient(serviceBusConnectionString);
            var sender = client.CreateSender(QueueName);

            try
            {
                var message = new ServiceBusMessage(name);
                await sender.SendMessageAsync(message);
                logger.LogInformation($"Message envoyé à la queue pour le fichier {name}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Erreur lors de l'envoi du message : {ex.Message}");
            }
            finally
            {
                await sender.DisposeAsync();
                await client.DisposeAsync();
            }
        }
    }
}
