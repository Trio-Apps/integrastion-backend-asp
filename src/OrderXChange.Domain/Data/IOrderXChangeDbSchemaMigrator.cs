using System.Threading.Tasks;

namespace OrderXChange.Data;

public interface IOrderXChangeDbSchemaMigrator
{
    Task MigrateAsync();
}
