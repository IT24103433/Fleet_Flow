using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Messaging;
using IdentityService.Api.Messaging.Events;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FleetFlow.IdentityService.Tests;

public class IdentityKafkaProducerTests
{
    private class MockKafkaProducer : IKafkaProducerService
    {
        public List<(string Topic, string Key, object Message)> PublishedEvents { get; } = new();

        public Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add((topic, key, message!));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task KafkaProducerService_WhenDisabled_DoesNotThrowAndCompletes()
    {
        var settings = Options.Create(new KafkaSettings
        {
            Enabled = false,
            BootstrapServers = "localhost:9092"
        });
        var logger = NullLogger<KafkaProducerService>.Instance;
        using var producer = new KafkaProducerService(settings, logger);

        var statusEvent = new UserStatusChangedEvent
        {
            UserId = Guid.NewGuid(),
            Username = "test_user",
            Email = "test@fleetflow.io",
            IsActive = false,
            Status = "DISABLED"
        };

        var exception = await Record.ExceptionAsync(() => producer.PublishAsync("fleetflow.user.events", statusEvent.UserId.ToString(), statusEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task KafkaProducerService_WhenUnreachableBroker_GracefullyHandlesFailureWithoutThrowing()
    {
        var settings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            BootstrapServers = "127.0.0.1:1" // Unreachable port
        });
        var logger = NullLogger<KafkaProducerService>.Instance;
        using var producer = new KafkaProducerService(settings, logger);

        var statusEvent = new UserStatusChangedEvent
        {
            UserId = Guid.NewGuid(),
            Username = "test_user_2",
            Email = "test2@fleetflow.io",
            IsActive = true,
            Status = "ACTIVE"
        };

        var exception = await Record.ExceptionAsync(() => producer.PublishAsync("fleetflow.user.events", statusEvent.UserId.ToString(), statusEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task AdminUserService_SetUserStatus_PublishesUserStatusChangedEventToKafka()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new IdentityDbContext(options);

        var role = new Role { Id = Guid.NewGuid(), Name = "CUSTOMER" };
        dbContext.Roles.Add(role);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "status_kafka_user",
            Email = "status_kafka@example.com",
            FullName = "Status Kafka User",
            IsActive = true
        };
        user.Roles.Add(role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var mockProducer = new MockKafkaProducer();
        var kafkaSettings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            UserEventsTopic = "fleetflow.user.events"
        });
        var passwordHasher = new PasswordHasher<User>();
        var service = new AdminUserService(dbContext, passwordHasher, mockProducer, kafkaSettings);

        var adminId = Guid.NewGuid();
        var response = await service.SetUserStatusAsync(user.Id, false, adminId);

        response.Should().NotBeNull();
        response.IsActive.Should().BeFalse();
        response.Status.Should().Be("DISABLED");

        mockProducer.PublishedEvents.Should().HaveCount(1);
        var published = mockProducer.PublishedEvents.First();
        published.Topic.Should().Be("fleetflow.user.events");
        published.Key.Should().Be(user.Id.ToString());

        var eventPayload = published.Message as UserStatusChangedEvent;
        eventPayload.Should().NotBeNull();
        eventPayload!.UserId.Should().Be(user.Id);
        eventPayload.Username.Should().Be("status_kafka_user");
        eventPayload.IsActive.Should().BeFalse();
        eventPayload.Status.Should().Be("DISABLED");
        eventPayload.ChangedByAdminId.Should().Be(adminId);
    }

    [Fact]
    public async Task AdminUserService_CreateUser_PublishesUserCreatedEventToKafka()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new IdentityDbContext(options);

        var role = new Role { Id = Guid.NewGuid(), Name = "FLEET_MANAGER" };
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        var mockProducer = new MockKafkaProducer();
        var kafkaSettings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            UserEventsTopic = "fleetflow.user.events"
        });
        var passwordHasher = new PasswordHasher<User>();
        var service = new AdminUserService(dbContext, passwordHasher, mockProducer, kafkaSettings);

        var request = new CreateAdminUserRequest
        {
            FullName = "Kafka Provisioned Manager",
            Username = "kafka_manager",
            Email = "kafka_manager@fleetflow.io",
            PhoneNumber = "+1-555-0811",
            Address = "811 Kafka Parkway",
            Role = "FLEET_MANAGER",
            InitialPassword = "Password@123"
        };

        var response = await service.CreateUserAsync(request);

        response.Should().NotBeNull();
        response.Username.Should().Be("kafka_manager");

        mockProducer.PublishedEvents.Should().HaveCount(1);
        var published = mockProducer.PublishedEvents.First();
        published.Topic.Should().Be("fleetflow.user.events");

        var eventPayload = published.Message as UserCreatedEvent;
        eventPayload.Should().NotBeNull();
        eventPayload!.UserId.Should().Be(response.Id);
        eventPayload.Username.Should().Be("kafka_manager");
        eventPayload.Email.Should().Be("kafka_manager@fleetflow.io");
        eventPayload.Role.Should().Be("FLEET_MANAGER");
    }

    [Fact]
    public async Task AdminUserService_UpdateUser_PublishesUserUpdatedEventToKafka()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new IdentityDbContext(options);

        var role1 = new Role { Id = Guid.NewGuid(), Name = "CUSTOMER" };
        var role2 = new Role { Id = Guid.NewGuid(), Name = "MAINTENANCE_STAFF" };
        dbContext.Roles.AddRange(role1, role2);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "kafka_updater",
            Email = "updater@fleetflow.io",
            FullName = "Original Name",
            IsActive = true
        };
        user.Roles.Add(role1);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var mockProducer = new MockKafkaProducer();
        var kafkaSettings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            UserEventsTopic = "fleetflow.user.events"
        });
        var passwordHasher = new PasswordHasher<User>();
        var service = new AdminUserService(dbContext, passwordHasher, mockProducer, kafkaSettings);

        var updateRequest = new UpdateAdminUserRequest
        {
            FullName = "Updated Kafka Name",
            Username = "kafka_updater",
            Email = "updated_kafka@fleetflow.io",
            PhoneNumber = "+1-555-9000",
            Address = "900 New Way",
            Role = "MAINTENANCE_STAFF"
        };

        var response = await service.UpdateUserAsync(user.Id, updateRequest, Guid.NewGuid());

        response.Should().NotBeNull();
        response.FullName.Should().Be("Updated Kafka Name");

        mockProducer.PublishedEvents.Should().HaveCount(1);
        var published = mockProducer.PublishedEvents.First();
        published.Topic.Should().Be("fleetflow.user.events");

        var eventPayload = published.Message as UserUpdatedEvent;
        eventPayload.Should().NotBeNull();
        eventPayload!.UserId.Should().Be(user.Id);
        eventPayload.FullName.Should().Be("Updated Kafka Name");
        eventPayload.Role.Should().Be("MAINTENANCE_STAFF");
    }

    [Fact]
    public async Task RegistrationService_Register_PublishesUserCreatedEventToKafka()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new IdentityDbContext(options);

        var customerRole = new Role { Id = Guid.NewGuid(), Name = "CUSTOMER" };
        dbContext.Roles.Add(customerRole);
        await dbContext.SaveChangesAsync();

        var mockProducer = new MockKafkaProducer();
        var kafkaSettings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            UserEventsTopic = "fleetflow.user.events"
        });
        var passwordHasher = new PasswordHasher<User>();
        var regService = new RegistrationService(dbContext, passwordHasher, mockProducer, kafkaSettings);

        var regRequest = new RegisterRequest
        {
            FullName = "Self Registered User",
            Username = "self_registered",
            Email = "self_reg@example.com",
            PhoneNumber = "+1-555-1234",
            Address = "123 Registration Lane",
            DrivingLicenseNumber = "DL-REG-1234",
            Password = "Password@123",
            ConfirmPassword = "Password@123"
        };

        var response = await regService.RegisterAsync(regRequest);

        response.Should().NotBeNull();
        response.Username.Should().Be("self_registered");

        mockProducer.PublishedEvents.Should().HaveCount(1);
        var published = mockProducer.PublishedEvents.First();
        published.Topic.Should().Be("fleetflow.user.events");

        var eventPayload = published.Message as UserCreatedEvent;
        eventPayload.Should().NotBeNull();
        eventPayload!.UserId.Should().Be(response.Id);
        eventPayload.Username.Should().Be("self_registered");
        eventPayload.Role.Should().Be("CUSTOMER");
    }
}
