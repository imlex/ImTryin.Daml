using Com.Daml.Ledger.Api.V1;
using Com.Daml.Ledger.Api.V1.Admin;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ImTryin.Daml.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ImTryin.Daml.Dump;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddDamlConnectionOptions().BindConfiguration("").ValidateDataAnnotations();
                services.AddDamlServiceClients();

                services.AddOptions<DumpOptions>().BindConfiguration("").ValidateDataAnnotations();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Dupm app started...");

        var dumpOptions = host.Services.GetRequiredService<IOptions<DumpOptions>>().Value;

        if (File.Exists(dumpOptions.OutputFile) && !dumpOptions.Force)
        {
            logger.LogWarning($"'{dumpOptions.OutputFile}' file exists! Specify --Force argument to overwrite.");
            return;
        }

        var damlConnectionRuntimeOptions = host.Services.GetRequiredService<IOptions<DamlConnectionOptions>>().Value.Runtime!;
        string subject;

        if (damlConnectionRuntimeOptions.PayloadV1 != null)
            subject = damlConnectionRuntimeOptions.PayloadV1.Data.ActAs[0];
        else if (damlConnectionRuntimeOptions.PayloadV2 != null)
            subject = damlConnectionRuntimeOptions.PayloadV2.Sub;
        else
            throw new NotSupportedException("'" + damlConnectionRuntimeOptions + "' is not supported!");

        var ledgerId = damlConnectionRuntimeOptions.PayloadV1?.Data.LedgerId ?? string.Empty;

        using var binaryWriter = new BinaryWriter(File.Create(dumpOptions.OutputFile));

        new StringValue {Value = subject}.WriteDelimitedTo(binaryWriter.BaseStream);
        new StringValue {Value = ledgerId}.WriteDelimitedTo(binaryWriter.BaseStream);

        var packageServiceClient = host.Services.GetRequiredService<PackageService.PackageServiceClient>();
        var packageIds = (await packageServiceClient.ListPackagesAsync(
                new ListPackagesRequest
                {
                    LedgerId = ledgerId
                }))
            .PackageIds;
        binaryWriter.Write(packageIds.Count);
        foreach (var packageId in packageIds)
        {
            new StringValue {Value = packageId}.WriteDelimitedTo(binaryWriter.BaseStream);

            var getPackageResponse = await packageServiceClient.GetPackageAsync(
                new GetPackageRequest
                {
                    LedgerId = ledgerId,
                    PackageId = packageId
                });

            getPackageResponse.WriteDelimitedTo(binaryWriter.BaseStream);
        }

        var transactionServiceClient = host.Services.GetRequiredService<TransactionService.TransactionServiceClient>();
        var ledgerEndOffset = (await transactionServiceClient.GetLedgerEndAsync(
                new GetLedgerEndRequest
                {
                    LedgerId = ledgerId
                }))
            .Offset;

        var transactionFilter = new TransactionFilter();
        var emptyFilters = new Filters();
        if (damlConnectionRuntimeOptions.PayloadV1 != null)
        {
            foreach (var party in damlConnectionRuntimeOptions.PayloadV1.Data.ActAs)
                transactionFilter.FiltersByParty.Add(party, emptyFilters);

            foreach (var party in damlConnectionRuntimeOptions.PayloadV1.Data.ReadAs)
                transactionFilter.FiltersByParty.Add(party, emptyFilters);
        }
        else if (damlConnectionRuntimeOptions.PayloadV2 != null)
        {
            var userManagementServiceClient = host.Services.GetRequiredService<UserManagementService.UserManagementServiceClient>();
            var rights = (await userManagementServiceClient.ListUserRightsAsync(new ListUserRightsRequest {UserId = damlConnectionRuntimeOptions.PayloadV2.Sub}))
                .Rights;

            foreach (var right in rights)
            {
                switch (right.KindCase)
                {
                    case Right.KindOneofCase.CanActAs:
                        transactionFilter.FiltersByParty.Add(right.CanActAs.Party, emptyFilters);
                        break;
                    case Right.KindOneofCase.CanReadAs:
                        transactionFilter.FiltersByParty.Add(right.CanReadAs.Party, emptyFilters);
                        break;
                }
            }
        }

        using var transactionTreesCall = transactionServiceClient.GetTransactionTrees(new GetTransactionsRequest
        {
            LedgerId = ledgerId,
            Begin = new LedgerOffset {Boundary = LedgerOffset.Types.LedgerBoundary.LedgerBegin},
            End = ledgerEndOffset,
            Filter = transactionFilter,
            Verbose = true
        });

        var transactionCount = 0;
        await foreach (var transactionTreesResponse in transactionTreesCall.ResponseStream.ReadAllAsync())
        {
            transactionTreesResponse.WriteDelimitedTo(binaryWriter.BaseStream);
            transactionCount++;
        }

        logger.LogWarning($"Written {transactionCount} transactions.");
    }
}