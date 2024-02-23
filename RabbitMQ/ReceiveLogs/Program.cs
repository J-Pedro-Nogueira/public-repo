using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

//comum a cliente e servidor:
var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.ExchangeDeclare(exchange: "EVENTS", type: ExchangeType.Direct); //!/exchange deve ter nome EVENTS/!//
//

//exclusivo ao cliente:
var queueName = channel.QueueDeclare().QueueName;
//

//identificacao deste cliente:
string clientId = "clientA"; //!/clientId deve ser igual ao username do client/!//
//

//ligar cliente ao broker:
channel.QueueBind(queue: queueName,
                  exchange: "EVENTS",
                  routingKey: clientId);
//

Console.WriteLine(" [*] Waiting for messages.");

//exclusivo ao cliente:
var consumer = new EventingBasicConsumer(channel);
//

//logica despoletada quando chega uma mensagem:
consumer.Received += (model, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    var routingKey = ea.RoutingKey;
    Console.WriteLine($" [x] Received '{routingKey}':'{message}'");
};
//

//obrigar a esperar por mensagens do broker:
channel.BasicConsume(queue: queueName,
                     autoAck: true,
                     consumer: consumer);
//

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();