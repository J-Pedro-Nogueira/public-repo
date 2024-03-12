using Provision;
using Grpc.Core;
using Microsoft.AspNetCore.Hosting.Server;
using System.Text;
using RabbitMQ.Client;

namespace Provision
{
    class Program
    {
        static void Main(string[] args)
        {
            const int Port = 50051;

            Server server = new Server
            {
                Services = { Provisioning.BindService(new ProvisioningService())},
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }

            };

            server.Start();

            Console.WriteLine($"Server listening on port {Port}");
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();

        }
    }
}