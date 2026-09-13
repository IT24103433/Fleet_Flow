using System.Threading;
using System.Threading.Tasks;

namespace FleetService.Api.Messaging;

public interface IKafkaProducerService
{
    Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
}
