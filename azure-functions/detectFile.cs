using System;
using System.IO;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;

public static class BlobTriggeredFunction
{
    private const string QueueName = "imagequeue";

    [Function("BlobTriggeredFunction")]
    public static async Task Run(
        [BlobTrigger("images/{name}", Connection = "AzureWebJobsStorage")] Stream blob,
        string name,
        ILogger log)
    {
        log.LogInformation($"Blob triggered: {name}, Size: {blob.Length} bytes");

        var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
        await using var client = new ServiceBusClient(serviceBusConnectionString);
        var sender = client.CreateSender(QueueName);

        try
        {
            var message = new ServiceBusMessage(name);
            await sender.SendMessageAsync(message);
            log.LogInformation($"Message envoyé à la queue pour le fichier {name}");
        }
        catch (Exception ex)
        {
            log.LogError($"Erreur lors de l'envoi du message : {ex.Message}");
        }
        finally
        {
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }
    }
}
