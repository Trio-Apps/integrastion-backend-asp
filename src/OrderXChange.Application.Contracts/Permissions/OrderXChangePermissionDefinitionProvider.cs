using OrderXChange.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Permissions;

public class OrderXChangePermissionDefinitionProvider : PermissionDefinitionProvider
{
    private const string AbpAuditLoggingGroup = "AbpAuditLogging";
    private const string AbpSettingManagementGroup = "AbpSettingManagement";

    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(OrderXChangePermissions.GroupName);

        myGroup.AddPermission(OrderXChangePermissions.Dashboard.Host, L("Permission:Dashboard"), MultiTenancySides.Host);
        myGroup.AddPermission(OrderXChangePermissions.Dashboard.Tenant, L("Permission:Dashboard"), MultiTenancySides.Tenant);

        // Phase 2 — Orders Console
        myGroup.AddPermission(OrderXChangePermissions.Orders.Default, L("Permission:Orders"), MultiTenancySides.Tenant);

        // Phase 2 — Item & Modifier Availability (Manage is a child of the view permission)
        var availability = myGroup.AddPermission(
            OrderXChangePermissions.Availability.Default, L("Permission:Availability"), MultiTenancySides.Tenant);
        availability.AddChild(OrderXChangePermissions.Availability.Manage, L("Permission:Availability.Manage"));

        // Account management (tenant)
        myGroup.AddPermission(OrderXChangePermissions.FoodicsAccounts.Default, L("Permission:FoodicsAccounts"), MultiTenancySides.Tenant);
        myGroup.AddPermission(OrderXChangePermissions.TalabatAccounts.Default, L("Permission:TalabatAccounts"), MultiTenancySides.Tenant);

        // Phase 2 — branch-scoped authorization management
        myGroup.AddPermission(OrderXChangePermissions.Branches.Manage, L("Permission:Branches.Manage"), MultiTenancySides.Tenant);
        myGroup.AddPermission(OrderXChangePermissions.Branches.All, L("Permission:Branches.All"), MultiTenancySides.Tenant);
    }

    // Hide ABP framework permission groups this app doesn't use, so the role/user permission
    // screen only shows the relevant ones (Identity management + OrderXChange).
    public override void PostDefine(IPermissionDefinitionContext context)
    {
        // Audit logging — no audit-log page is exposed.
        if (context.GetGroupOrNull(AbpAuditLoggingGroup) != null)
        {
            context.RemoveGroup(AbpAuditLoggingGroup);
        }

        // Setting management — only the generic ABP "Time zone" permission surfaces here, and the
        // app uses its own config pages (not the ABP settings UI), so drop the whole group.
        if (context.GetGroupOrNull(AbpSettingManagementGroup) != null)
        {
            context.RemoveGroup(AbpSettingManagementGroup);
        }
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<OrderXChangeResource>(name);
    }
}
