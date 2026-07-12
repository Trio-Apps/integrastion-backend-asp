using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace OrderXChange.Data;

// Idempotent: grants all Phase 2 permissions to the admin role on every migrator run.
// Must execute before branch-scope enforcement or admin will be locked out.
public class Phase2PermissionsDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    // All permissions defined in OrderXChangePermissionDefinitionProvider.
    private static readonly string[] AdminPermissions =
    {
        "OrderXChange.Dashboard.Host",
        "OrderXChange.Dashboard.Tenant",
        "OrderXChange.Orders",
        "OrderXChange.Availability",
        "OrderXChange.Availability.Manage",
        "OrderXChange.FoodicsAccounts",
        "OrderXChange.TalabatAccounts",
        "OrderXChange.Branches.Manage",
        "OrderXChange.Branches.All"
    };

    private readonly IPermissionDataSeeder _permissionDataSeeder;

    public Phase2PermissionsDataSeedContributor(IPermissionDataSeeder permissionDataSeeder)
    {
        _permissionDataSeeder = permissionDataSeeder;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        await _permissionDataSeeder.SeedAsync(
            "R", // RolePermissionValueProvider.ProviderName
            "admin",
            AdminPermissions,
            context.TenantId
        );
    }
}
