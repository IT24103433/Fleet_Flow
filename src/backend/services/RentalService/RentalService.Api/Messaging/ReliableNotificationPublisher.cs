using Confluent.Kafka;
using Microsoft.Extensions.Options;
namespace RentalService.Api.Messaging;
public interface IReliableNotificationPublisher { Task PublishAsync(string topic,Guid eventId,string payload,CancellationToken ct); }
public sealed class ReliableNotificationPublisher(IOptions<KafkaSettings> settings):IReliableNotificationPublisher,IDisposable
{
    private readonly Lazy<IProducer<string,string>> producer=new(()=>new ProducerBuilder<string,string>(new ProducerConfig
    { BootstrapServers=settings.Value.BootstrapServers,ClientId=settings.Value.ClientId+"-outbox",Acks=Acks.All,
      EnableIdempotence=true,MessageTimeoutMs=5000,SocketTimeoutMs=5000 }).Build());
    public async Task PublishAsync(string topic,Guid eventId,string payload,CancellationToken ct)
    {
        var result=await producer.Value.ProduceAsync(topic,new Message<string,string>{Key=eventId.ToString(),Value=payload},ct);
        if(result.Status!=PersistenceStatus.Persisted)throw new InvalidOperationException("Kafka did not acknowledge durable persistence.");
    }
    public void Dispose() { if(producer.IsValueCreated) producer.Value.Dispose(); }
}
