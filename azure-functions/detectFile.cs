using System;
using System.IO;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;

public static class BlobTriggeredFunction
{
    // Remplacez <...> avec vos informations d'identification
    private const string ServiceBusConnectionString = "Endpoint=sb://<NAMESPACE>.servicebus.windows.net/;SharedAccessKeyName=<KEY_NAME>;SharedAccessKey=<KEY>";
    private const string QueueName = "imagequeue";

    [Function("BlobTriggeredFunction")]
    public static void Run(
        [BlobTrigger("sample-container/{name}")] Stream blob,
        string name,
        ILogger log)
    {
        log.LogInformation($"Blob triggered: {name}, Size: {blob.Length} bytes");

        // Ajouter le nom du fichier dans la queue Azure Service Bus
        ServiceBusClient client = new(ServiceBusConnectionString);
        ServiceBusSender sender = client.CreateSender(QueueName);

        try
        {
            ServiceBusMessage message = new(name);
            sender.SendMessageAsync(message).GetAwaiter().GetResult();
            log.LogInformation($"Message envoyé à la queue pour le fichier {name}");
        }
        catch (Exception ex)
        {
            log.LogError($"Erreur lors de l'envoi du message : {ex.Message}");
        }
        finally
        {
            sender.DisposeAsync().GetAwaiter().GetResult();
            client.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
