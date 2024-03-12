using Grpc.Core;
using System;
using Client;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics.Tracing;
using System.Data.SqlClient;

namespace Client
{
    public class Program
   {
        static void Main(string[] args)
        {
            Console.WriteLine("Type in username.");
            string username = Console.ReadLine();

            Console.WriteLine("Type in password.");
            string password = Console.ReadLine();

            bool AdminPrivilieges = false;
            if(username=="Admin" && password == "Admin") AdminPrivilieges=true; //client privilegiado para aceder a server

            ////////////////////////////////////////////////////////////////////////////////////////por cliente a escuta do broker
            //comum a cliente e servidor:
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var MQchannel = connection.CreateModel();

            MQchannel.ExchangeDeclare(exchange: "EVENTS", type: ExchangeType.Direct); //!/exchange deve ter nome EVENTS/!//
                                                                                      //

            //exclusivo ao cliente:
            var queueName = MQchannel.QueueDeclare().QueueName;
            //

            //identificacao deste cliente:
            string clientId = username + password; //!/clientId deve ser igual ao username + pass do client/!//
            //

            //ligar cliente ao broker:
            MQchannel.QueueBind(queue: queueName,
                              exchange: "EVENTS",
                              routingKey: clientId);
            //

            //exclusivo ao cliente:
            var consumer = new EventingBasicConsumer(MQchannel);
            //

            //logica despoletada quando chega uma mensagem:
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var routingKey = ea.RoutingKey;
                Console.WriteLine($"[X] Notification received:'{message}'");
                ProcessIncomingMessage(message);
            };
            //

            MQchannel.BasicConsume(queue: queueName,
                                 autoAck: true,
                                 consumer: consumer);
            //
            //////////////////////////////////////////////////////////////////////////////////////////por cliente a escuta do broker
            
            string command = "";
            while(command!="quit")
            {
                if (!AdminPrivilieges)
                {
                    Console.WriteLine("Type in command [Reserve]/[Activate]/[Deactivate]/[Terminate]/[Quit].");
                    command = Console.ReadLine().ToLower();

                    switch (command)
                    {
                        case "reserve":
                            Reserve(username, password);
                            break;
                        case "activate":
                            Activate(username, password);
                            break;
                        case "deactivate":
                            Deactivate(username, password);
                            break;
                        case "terminate":
                            Terminate(username, password);
                            break;
                    }
                }
                if (AdminPrivilieges)
                {
                    Console.WriteLine("Type in command [ListCoverage]/[ShowProcesses].");
                    command = Console.ReadLine().ToLower();

                    switch (command)
                    {
                        case "listcoverage":
                            ListCoverage();
                            break;
                        case "showprocesses":
                            ShowProcessesAsync(username, password);
                            break;
                    }
                }

            }//escolha dos comandos pelo cliente


            //channel.ShutdownAsync().Wait();             
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

        }

        static void Reserve(string username, string password)
        {
            Console.WriteLine("Type in domicile name.");
            string domicilio = Console.ReadLine();

            Console.WriteLine("Type in modality.");
            string modalidade = Console.ReadLine();

            const string Host = "localhost";
            const int Port = 50051;

            var channel = new Channel($"{Host}:{Port}", ChannelCredentials.Insecure);
            var client = new Provisioning.ProvisioningClient(channel);


            var request_reserve = new ReserveRequest { Modalidade = modalidade, Domicilio = domicilio, Username = username, Pass = password };
            var response_reserve = client.Reserve(request_reserve);

            if (response_reserve.NumAdministrativo.Contains("ERROR"))//tratamento de erros
            {
                Console.WriteLine($"{response_reserve.NumAdministrativo}");
            }
            if (!response_reserve.NumAdministrativo.Contains("ERROR"))
            {
                Console.WriteLine($"Administrative Number is {response_reserve.NumAdministrativo}");

                //consulta a db
                string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_ClientA_test_db;Integrated Security=True";
                SqlConnection Con = new SqlConnection(connection_string);
                Con.Open();

                string query_string = "UPDATE Domicilios SET Estado = 'RESERVED', Numero_administrativo = '"
                    + response_reserve.NumAdministrativo + "', Operadora = '" + username + "', Modalidade = '" + modalidade + "' WHERE Nome = '" + domicilio + "';";
                SqlCommand cmd = new SqlCommand(query_string, Con);
                cmd.ExecuteNonQuery();

                Con.Close();

                Console.WriteLine($"Domicilie {domicilio} updated with Administrative Number {response_reserve.NumAdministrativo} on local database");
            }

            channel.ShutdownAsync().Wait();
        }

        static void Activate(string username, string password)
        {
            string admin_number = "?";
            while (admin_number.Contains('?'))
            {
                Console.WriteLine("Type in Administrative Number.\nType [?] to see numbers saved on local database");
                admin_number = Console.ReadLine();

                if (admin_number.Contains('?'))//
                {
                    //consulta a db
                    string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_ClientA_test_db;Integrated Security=True";
                    SqlConnection Con = new SqlConnection(connection_string);
                    Con.Open();

                    string query_string = "SELECT * FROM Domicilios WHERE Operadora = '" + username + "' AND Numero_Administrativo IS NOT NULL;";

                    SqlCommand cmd = new SqlCommand(query_string, Con);
                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        if (true)
                        {
                            Console.WriteLine($"{dr[1]} - {dr[3]}");
                        }
                    }
                    dr.Close();
                    Con.Close();
                }//consulta de numeros a db local
            }//input do user

            const string Host = "localhost";
            const int Port = 50051;

            var channel = new Channel($"{Host}:{Port}", ChannelCredentials.Insecure);
            var client = new Provisioning.ProvisioningClient(channel);

            var request_activation = new ActivationRequest { NumAdministrativo = admin_number, Username = username, Pass = password };
            var response_activation = client.Activation(request_activation);

            Console.WriteLine($"Can activate = {response_activation.CanActivate}.");

            if (response_activation.ExpectedActivationTime.Contains("ERROR"))
            {
                Console.WriteLine($"{response_activation.ExpectedActivationTime}");
            }
            if (!response_activation.ExpectedActivationTime.Contains("ERROR"))
            {
                Console.WriteLine($"Expected wait time = {response_activation.ExpectedActivationTime} seconds.");
            }

            channel.ShutdownAsync().Wait();
        }

        static void Deactivate(string username, string password)
        {
            string admin_number = "?";
            while (admin_number.Contains('?'))
            {
                Console.WriteLine("Type in Administrative Number.\nType [?] to see numbers saved on local database");
                admin_number = Console.ReadLine();

                if (admin_number.Contains('?'))//
                {
                    //consulta a db
                    string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_ClientA_test_db;Integrated Security=True";
                    SqlConnection Con = new SqlConnection(connection_string);
                    Con.Open();

                    string query_string = "SELECT * FROM Domicilios WHERE Operadora = '" + username + "' AND Numero_Administrativo IS NOT NULL;";

                    SqlCommand cmd = new SqlCommand(query_string, Con);
                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        if (true)
                        {
                            Console.WriteLine($"{dr[1]} - {dr[3]}");
                        }
                    }
                    dr.Close();
                    Con.Close();
                }//consulta de numeros a db local
            }//input do user

            const string Host = "localhost";
            const int Port = 50051;

            var channel = new Channel($"{Host}:{Port}", ChannelCredentials.Insecure);
            var client = new Provisioning.ProvisioningClient(channel);


            var request_deactivation = new DeactivationRequest { NumAdministrativo = admin_number, Username = username, Pass = password };
            var response_deactivation = client.Deactivation(request_deactivation);

            Console.WriteLine($"Can deactivate = {response_deactivation.CanDeactivate}.");

            if (response_deactivation.ExpectedDeactivationTime.Contains("ERROR"))
            {
                Console.WriteLine($"{response_deactivation.ExpectedDeactivationTime}");
            }
            if (!response_deactivation.ExpectedDeactivationTime.Contains("ERROR"))
            {
                Console.WriteLine($"Expected wait time = {response_deactivation.ExpectedDeactivationTime} seconds.");
            }

            channel.ShutdownAsync().Wait();
        }

        static void Terminate(string username, string password)
        {
            string admin_number = "?";
            while (admin_number.Contains('?'))
            {
                Console.WriteLine("Type in Administrative Number.\nType [?] to see numbers saved on local database");
                admin_number = Console.ReadLine();

                if (admin_number.Contains('?'))//
                {
                    //consulta a db
                    string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_ClientA_test_db;Integrated Security=True";
                    SqlConnection Con = new SqlConnection(connection_string);
                    Con.Open();

                    string query_string = "SELECT * FROM Domicilios WHERE Operadora = '" + username + "' AND Numero_Administrativo IS NOT NULL;";

                    SqlCommand cmd = new SqlCommand(query_string, Con);
                    SqlDataReader dr = cmd.ExecuteReader();

                    while (dr.Read())
                    {
                        if (true)
                        {
                            Console.WriteLine($"{dr[1]} - {dr[3]}");
                        }
                    }
                    dr.Close();
                    Con.Close();
                }//consulta de numeros a db local
            }//input do user

            const string Host = "localhost";
            const int Port = 50051;

            var channel = new Channel($"{Host}:{Port}", ChannelCredentials.Insecure);
            var client = new Provisioning.ProvisioningClient(channel);


            var request_termination = new TerminationRequest { NumAdministrativo = admin_number, Username = username, Pass = password };
            var response_termination = client.Termination(request_termination);

            Console.WriteLine($"Can terminate = {response_termination.CanTerminate}");

            if (response_termination.ExpectedTerminationTime.Contains("ERROR"))
            {
                Console.WriteLine($"{response_termination.ExpectedTerminationTime}");
            }
            if (!response_termination.ExpectedTerminationTime.Contains("ERROR"))
            {
                Console.WriteLine($"Expected wait time = {response_termination.ExpectedTerminationTime} seconds.");
            }

            channel.ShutdownAsync().Wait();
        }

        static void ProcessIncomingMessage(string message)
        {
            if(message.Contains("OK - Activation processed"))
            {
                string[] words = message.Split(' ', '.');
                string numadmin = words[words.Length - 2];

                //consulta a db
                string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_ClientA_test_db;Integrated Security=True";
                SqlConnection Con = new SqlConnection(connection_string);
                Con.Open();

                string query_string = "UPDATE Domicilios SET Estado = 'ACTIVE' WHERE Numero_administrativo = '" + numadmin + "';";
                SqlCommand cmd = new SqlCommand(query_string, Con);
                cmd.ExecuteNonQuery();

                Con.Close();
                Console.WriteLine("Address activation successfully updated on local database.");
            }
            if (message.Contains("OK - Deactivation processed"))
            {
                string[] words = message.Split(' ', '.');
                string numadmin = words[words.Length - 2];

                //consulta a db
                string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_ClientA_test_db;Integrated Security=True";
                SqlConnection Con = new SqlConnection(connection_string);
                Con.Open();

                string query_string = "UPDATE Domicilios SET Estado = 'DEACTIVATED' WHERE Numero_administrativo = '" + numadmin + "';";
                SqlCommand cmd = new SqlCommand(query_string, Con);
                cmd.ExecuteNonQuery();

                Con.Close();
                Console.WriteLine("Address deactivaton successfully updated on local database.");
            }
            if (message.Contains("OK - Termination processed"))
            {
                string[] words = message.Split(' ', '.');
                string numadmin = words[words.Length - 2];

                //consulta a db
                string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_ClientA_test_db;Integrated Security=True";
                SqlConnection Con = new SqlConnection(connection_string);
                Con.Open();

                string query_string = "UPDATE Domicilios SET Estado = 'FREE', Operadora = NULL, Modalidade = NULL WHERE Numero_administrativo = '" + numadmin + "';";
                SqlCommand cmd = new SqlCommand(query_string, Con);
                cmd.ExecuteNonQuery();

                Con.Close();
                Console.WriteLine("Address termination successfully updated on local database.");
            }

        }

        static void ListCoverage()
        {
            //consulta a db
            string connection_string = "Data Source=DESKTOP-SUAMV24\\SQLEXPRESS;Initial Catalog=SD_test_db;Integrated Security=True";
            SqlConnection Con = new SqlConnection(connection_string);
            Con.Open();

            string query_string = "SELECT * FROM Domicilios";
            SqlCommand cmd = new SqlCommand(query_string, Con);
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                Console.WriteLine($"{dr[0]} - {dr[1]} - {dr[2]} - {dr[3]} - {dr[4]} - {dr[5]} - {dr[6]};");
            }
            dr.Close();
            //

            Con.Close();
        }

        static void ShowProcessesAsync(string username, string password)
        {

            const string Host = "localhost";
            const int Port = 50051;

            var channel = new Channel($"{Host}:{Port}", ChannelCredentials.Insecure);
            var client = new Provisioning.ProvisioningClient(channel);


            var request_logs= new GetLogsRequest { Username = username, Pass = password };
            var response_logs = client.GetLogs(request_logs);

            string logs = response_logs.Logs;

            Console.WriteLine(logs);



        }

    }
}