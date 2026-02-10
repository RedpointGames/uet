namespace Io.Mappers
{
    using System.Threading.Tasks;

    public interface IMapper<TFrom, TTo>
    {
        Task<TTo?> Map(TFrom? source, MapperContext context);
    }
}
