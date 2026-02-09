using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Infrastructure.Persistence;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration testing with mocked dependencies
/// Uses Decorator pattern with mocked IDataProvider
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IDataProvider _mockDataProvider;
    private readonly IDataRepository _mockRepository;

    public TestWebApplicationFactory(
        IDataProvider mockDataProvider,
        IDataRepository mockRepository)
    {
        _mockDataProvider = mockDataProvider;
        _mockRepository = mockRepository;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing registrations
            services.RemoveAll<IDataProvider>();
            services.RemoveAll<IDataRepository>();
            services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
            services.RemoveAll<CosmosClient>();

            // Remove CosmosDbInitializationService (IHostedService) to prevent Cosmos DB connection attempts during tests
            // We need to remove the specific IHostedService implementation, not all IHostedService registrations
            var hostedServiceDescriptor = services
                .FirstOrDefault(d => d.ImplementationType == typeof(CosmosDbInitializationService));
            if (hostedServiceDescriptor != null)
            {
                services.Remove(hostedServiceDescriptor);
            }

            // Add mock services
            services.AddSingleton(_mockDataProvider);
            services.AddSingleton(_mockRepository);
        });
    }
}
