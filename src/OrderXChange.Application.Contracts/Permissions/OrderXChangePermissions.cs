namespace OrderXChange.Permissions;

public static class OrderXChangePermissions
{
    public const string GroupName = "OrderXChange";

    public static class Dashboard
    {
        public const string DashboardGroup = GroupName + ".Dashboard";
        public const string Host = DashboardGroup + ".Host";
        public const string Tenant = DashboardGroup + ".Tenant";
    }

    // Phase 2 — Orders Console (view/monitor Talabat orders)
    public static class Orders
    {
        public const string Default = GroupName + ".Orders";
    }

    // Phase 2 — Item & Modifier Availability
    public static class Availability
    {
        public const string Default = GroupName + ".Availability";
        public const string Manage = Default + ".Manage"; // toggle In/Out of Stock
    }

    // Foodics account management (tenant)
    public static class FoodicsAccounts
    {
        public const string Default = GroupName + ".FoodicsAccounts";
    }

    // Talabat account management (tenant)
    public static class TalabatAccounts
    {
        public const string Default = GroupName + ".TalabatAccounts";
    }

    // Phase 2 — assign accessible branches to roles (branch-scoped authorization)
    public static class Branches
    {
        public const string Manage = GroupName + ".Branches.Manage";
    }
}
