using System.Text;
using RabbitMQ.Client;


//comum a cliente e servidor:
var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.ExchangeDeclare(exchange: "EVENTS", type: ExchangeType.Direct); //!/exchange deve ter nome EVENTS/!//
//

//identificacao do cliente que recebe este conteudo:
string clientId = "monmouth"; //!/clientId deve ser igual ao username do client/!//
//

//criacao da mensagem e envio para o broker:
var message = "test_message";
var body = Encoding.UTF8.GetBytes(message);
channel.BasicPublish(exchange: "reserve",
                     routingKey: clientId, //envia apenas para clientes com routingKey == clientId
                     basicProperties: null,
                     body: body);
//

Console.WriteLine($" [x] Sent to clientID = '{clientId}' the message: '{message}'");

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();