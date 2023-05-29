using RabbitMQ.Client;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor.Rabbit;

public interface IRabbitPublisher
{
    void Initialize(IModel channel);
    void Publish(string exchange, object data);
}
