﻿using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Grpc.Net.Client.Web;
using Tinkoff.InvestApi;

namespace Microsoft.Extensions.DependencyInjection;

public static class InvestApiClientExtensions
{
    private const string DefaultName = "";

    public static IServiceCollection AddInvestApiClient(this IServiceCollection services,
        Action<IServiceProvider, InvestApiSettings> configureSettings, bool isWebClient = false) =>
            AddInvestApiClient(services, DefaultName, configureSettings, isWebClient);

    public static IServiceCollection AddInvestApiClient(this IServiceCollection services, string name,
        Action<IServiceProvider, InvestApiSettings> configureSettings, bool isWebClient = false)
    {
        services.AddGrpcClient<InvestApiClient>(name,
                (serviceProvider, options) =>
                {
                    var settings = new InvestApiSettings();
                    configureSettings(serviceProvider, settings);
                    options.Address = settings.Sandbox
                        ? new Uri("https://sandbox-invest-public-api.tinkoff.ru:443")
                        : new Uri("https://invest-public-api.tinkoff.ru:443");
                })
            .ConfigureChannel((serviceProvider, options) =>
            {
                if (isWebClient)
                    options.HttpHandler = new GrpcWebHandler(new HttpClientHandler());

                options.MaxReceiveMessageSize = null;
                var settings = new InvestApiSettings();
                configureSettings(serviceProvider, settings);
                var accessToken = settings.AccessToken ??
                                  throw new InvalidOperationException("AccessToken is required");
                var appName = string.IsNullOrEmpty(settings.AppName)
                    ? "tinkoff.invest-api-csharp-sdk"
                    : settings.AppName;
                var credentials = CallCredentials.FromInterceptor((_, metadata) =>
                {
                    metadata.Add("Authorization", $"Bearer {accessToken}");
                    if (appName != null)
                    {
                        metadata.Add("x-app-name", appName);
                    }

                    return Task.CompletedTask;
                });

                options.Credentials = ChannelCredentials.Create(new SslCredentials(), credentials);

                var defaultMethodConfig = new MethodConfig
                {
                    Names = {MethodName.Default},
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 5,
                        InitialBackoff = TimeSpan.FromSeconds(1),
                        MaxBackoff = TimeSpan.FromSeconds(5),
                        BackoffMultiplier = 1.5,
                        RetryableStatusCodes = {StatusCode.Unavailable}
                    }
                };

                options.ServiceConfig = new ServiceConfig {MethodConfigs = {defaultMethodConfig}};
            });
        return services;
    }
}