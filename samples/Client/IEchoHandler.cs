using System.Threading.Tasks;

namespace Sample
{
    public interface IEchoHandler
    {
        Task<string> EchoAsync(string value);
    }
}
