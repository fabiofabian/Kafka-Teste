using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace KafkaDemo.Helpers.Kafka
{
    public interface IKafkaConsumer<T>
    {
        void Receive(Action<T> action);
    }

    public class KafkaConsumer<T> : IKafkaConsumer<T>, IDisposable
    {
        private readonly IConsumer<Ignore, string> _consumer;

        public KafkaConsumer(string bootstrapServers, string topic, string groupId)
        {
            var config = new ConsumerConfig
            {
                GroupId = groupId,
                BootstrapServers = bootstrapServers,
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            _consumer.Subscribe(topic);
        }

        public void Dispose()
        {
            _consumer.Dispose();
        }

        public void Receive(Action<T> action)
        {
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            Task.Run(() =>
            {
                try
                {
                    StartReceiving(action, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Closing consumer.");
                    _consumer.Close();
                }
            });
        }

        private void StartReceiving(Action<T> action, CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);
                    if (consumeResult.IsPartitionEOF)
                    {
                        continue;
                    }

                    var message = JsonConvert.DeserializeObject<T>(consumeResult.Value);
                    action(message);
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Consume error: {e.Error.Reason}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error inside action: {ex}");
                }
            }
        }
    }
}
