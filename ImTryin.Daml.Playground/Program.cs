using Com.Daml.Ledger.Api.V1;
using Grpc.Net.Client;

Console.WriteLine(@"Hello, Watcher! This is ImTryin's Daml Playground project.
All sample codes will be extracted to separate projects.");

using var grpcChannel = GrpcChannel.ForAddress("http://localhost:6865");
var ledgerIdentityServiceClient = new LedgerIdentityService.LedgerIdentityServiceClient(grpcChannel);
var getLedgerIdentityResponse = await ledgerIdentityServiceClient.GetLedgerIdentityAsync(new());

#pragma warning disable CS0612
Console.WriteLine($"Ledger identity = {getLedgerIdentityResponse.LedgerId}");
#pragma warning restore CS0612
Console.WriteLine("Press any key to exit...");
Console.ReadKey(true);
