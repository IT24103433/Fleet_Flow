using FleetFlow.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationService.Api.Messaging;
using NotificationService.Api.Services;
namespace NotificationService.Tests;
public sealed class ConsumerActivationTests
{
    [Fact]public async Task KafkaConsumerRequiresVerifiedMigrationReadinessBeforeStarting()
    {
        var probe=new Probe();var services=new ServiceCollection();services.AddSingleton(new NotificationActivation(true,Guid.NewGuid()));
        services.AddSingleton<IDatabaseReadiness>(probe);using var provider=services.BuildServiceProvider();
        using var consumer=new NotificationKafkaConsumer(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new KafkaSettings{Enabled=true,BootstrapServers="invalid-test-destination"}),NullLogger<NotificationKafkaConsumer>.Instance);
        await consumer.StartAsync(default);await probe.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));await consumer.StopAsync(default);
        Assert.True(probe.Called.Task.IsCompletedSuccessfully);
    }
    private sealed class Probe:IDatabaseReadiness
    {
        public TaskCompletionSource Called=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> IsReadyAsync(CancellationToken ct){Called.TrySetResult();return Task.FromResult(false);}
    }
}
