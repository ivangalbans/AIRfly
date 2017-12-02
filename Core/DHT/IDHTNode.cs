namespace Core.DHT
{
    public interface IDhtNode
    {
        string Host { get; set; }
        int Port { get; set; }
        ulong Id { get; }
    }
}
