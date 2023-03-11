using Com.Daml.Ledger.Api.V1;
using Com.Daml.Ledger.Api.V1.Admin;
using Com.Daml.Ledger.Api.V1.Testing;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using ImTryin.Daml.Api;
using ImTryin.Daml.Api.AccessTokens;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DamlServiceClientExtensions
{
    public static OptionsBuilder<DamlConnectionOptions> AddDamlConnectionOptions(this IServiceCollection services)
    {
        var optionsBuilder = services.AddOptions<DamlConnectionOptions>();

        services.PostConfigure<DamlConnectionOptions>(damlConnectionOptions =>
        {
            DamlConnectionOptions.RuntimeArgs runtime;

            if (damlConnectionOptions.AccessToken != null)
            {
                var payload = DamlAccessTokenUtil.Parse(damlConnectionOptions.AccessToken);

                if (payload is PayloadV1 payloadV1)
                {
                    runtime = new DamlConnectionOptions.RuntimeArgs
                    {
                        AccessToken = damlConnectionOptions.AccessToken,
                        PayloadV1 = payloadV1
                    };
                }
                else if (payload is PayloadV2 payloadV2)
                {
                    runtime = new DamlConnectionOptions.RuntimeArgs
                    {
                        AccessToken = damlConnectionOptions.AccessToken,
                        PayloadV2 = payloadV2
                    };
                }
                else
                {
                    throw new NotSupportedException("'" + payload + "' access token payload is not supported!");
                }
            }
            else if (damlConnectionOptions.Party != null)
            {
                var ledgerId = GetLedgerId(damlConnectionOptions);

                runtime = new DamlConnectionOptions.RuntimeArgs
                {
                    AccessToken = DamlAccessTokenUtil.GenerateSandboxTokenV1(ledgerId, damlConnectionOptions.ApplicationId,
                        damlConnectionOptions.Party, out var payloadV1),
                    PayloadV1 = payloadV1
                };
            }
            else if (damlConnectionOptions.V1 != null)
            {
                var ledgerId = GetLedgerId(damlConnectionOptions);

                var actAs = damlConnectionOptions.V1.ActAs.Split(';', StringSplitOptions.RemoveEmptyEntries);
                var readAs = damlConnectionOptions.V1.ReadAs.Split(';', StringSplitOptions.RemoveEmptyEntries);

                runtime = new DamlConnectionOptions.RuntimeArgs
                {
                    AccessToken = DamlAccessTokenUtil.GenerateSandboxTokenV1(ledgerId, damlConnectionOptions.ApplicationId,
                        damlConnectionOptions.V1.Admin, actAs, readAs, out var payloadV1),
                    PayloadV1 = payloadV1
                };
            }
            else if (damlConnectionOptions.V2 != null)
            {
                runtime = new DamlConnectionOptions.RuntimeArgs
                {
                    AccessToken = DamlAccessTokenUtil.GenerateSandboxTokenV2(damlConnectionOptions.V2.User, out var payloadV2),
                    PayloadV2 = payloadV2
                };
            }
            else
            {
                throw new NotSupportedException("'" + damlConnectionOptions + "' is not supported!");
            }

            damlConnectionOptions.Runtime = runtime;
        });

        return optionsBuilder;
    }

    private static string GetLedgerId(DamlConnectionOptions damlConnectionOptions)
    {
        using var grpcChannel = GrpcChannel.ForAddress(damlConnectionOptions.Address);
        var ledgerIdentityServiceClient = new LedgerIdentityService.LedgerIdentityServiceClient(grpcChannel);

#pragma warning disable CS0612
        var ledgerId = ledgerIdentityServiceClient.GetLedgerIdentity(new GetLedgerIdentityRequest()).LedgerId;
#pragma warning restore CS0612
        return ledgerId;
    }

    public static IServiceCollection AddDamlServiceClients(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddTransient<AccessTokenInterceptor>();

        services.AddGrpcClient<ConfigManagementService.ConfigManagementServiceClient>(ConfigureClient);
        services.AddGrpcClient<MeteringReportService.MeteringReportServiceClient>(ConfigureClient);
        services.AddGrpcClient<PackageManagementService.PackageManagementServiceClient>(ConfigureClient);
        services.AddGrpcClient<ParticipantPruningService.ParticipantPruningServiceClient>(ConfigureClient);
        services.AddGrpcClient<PartyManagementService.PartyManagementServiceClient>(ConfigureClient);
        services.AddGrpcClient<UserManagementService.UserManagementServiceClient>(ConfigureClient);

        services.AddGrpcClient<TimeService.TimeServiceClient>(ConfigureClient);

        services.AddGrpcClient<ActiveContractsService.ActiveContractsServiceClient>(ConfigureClient);
        services.AddGrpcClient<CommandCompletionService.CommandCompletionServiceClient>(ConfigureClient);
        services.AddGrpcClient<CommandService.CommandServiceClient>(ConfigureClient);
        services.AddGrpcClient<CommandSubmissionService.CommandSubmissionServiceClient>(ConfigureClient);
        services.AddGrpcClient<LedgerConfigurationService.LedgerConfigurationServiceClient>(ConfigureClient);
        services.AddGrpcClient<LedgerIdentityService.LedgerIdentityServiceClient>(ConfigureClient);
        services.AddGrpcClient<PackageService.PackageServiceClient>(ConfigureClient);
        services.AddGrpcClient<TransactionService.TransactionServiceClient>(ConfigureClient);
        services.AddGrpcClient<VersionService.VersionServiceClient>(ConfigureClient);

        return services;
    }

    private static void ConfigureClient(IServiceProvider serviceProvider, GrpcClientFactoryOptions options)
    {
        var damlConnectionOptions = serviceProvider.GetRequiredService<IOptions<DamlConnectionOptions>>().Value;

        options.Address = new Uri(damlConnectionOptions.Address);

        options.InterceptorRegistrations.Add(new InterceptorRegistration(InterceptorScope.Channel, sp => sp.GetRequiredService<AccessTokenInterceptor>()));
    }

    private class AccessTokenInterceptor : Interceptor
    {
        public AccessTokenInterceptor(IOptions<DamlConnectionOptions> options)
        {
            _damlConnectionOptions = options.Value;
        }

        private readonly DamlConnectionOptions _damlConnectionOptions;

        private ClientInterceptorContext<TRequest, TResponse> GetNewContext<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context)
            where TRequest : class
            where TResponse : class
        {
            if (_damlConnectionOptions.Runtime == null)
                return context;

            var headers = context.Options.Headers ?? new Metadata();

            headers.Add("Authorization", "Bearer " + _damlConnectionOptions.Runtime.AccessToken);

            return new ClientInterceptorContext<TRequest, TResponse>(
                context.Method,
                context.Host,
                context.Options.WithHeaders(headers));
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(request, GetNewContext(context));
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(request, GetNewContext(context));
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(request, GetNewContext(context));
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(GetNewContext(context));
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(GetNewContext(context));
        }
    }
}