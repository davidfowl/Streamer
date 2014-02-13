using System.Threading.Tasks;

namespace Client
{
    public interface IAdder
    {
        Task<int> Increment(int value);
    }
}
