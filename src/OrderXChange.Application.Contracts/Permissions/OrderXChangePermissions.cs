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

    // Phase 2 — branch-scoped authorization
    public static class Branches
    {
        // Assign accessible branches to roles (manage the role -> branch grants).
        public const string Manage = GroupName + ".Branches.Manage";

        // Bypass branch scoping entirely (see every branch). Grant to admin-type roles.
        public const string All = GroupName + ".Branches.All";
    }
}
