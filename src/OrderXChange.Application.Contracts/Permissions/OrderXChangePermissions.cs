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

    // Menu synchronization monitoring (the Menu Synchronization page)
    public static class MenuSync
    {
        public const string Default = GroupName + ".MenuSync";
    }

    // Sync run diagnostics (the Sync Diagnostics page)
    public static class SyncDiagnostics
    {
        public const string Default = GroupName + ".SyncDiagnostics";
    }

    // Catalog content pushed to Talabat — one permission per page
    public static class Categories
    {
        public const string Default = GroupName + ".Categories";
    }

    public static class PaymentMethods
    {
        public const string Default = GroupName + ".PaymentMethods";
    }

    public static class DeliveryCharges
    {
        public const string Default = GroupName + ".DeliveryCharges";
    }

    // Phase 2 — Orders Console (view/monitor Talabat orders)
    public static class Orders
    {
        public const string Default = GroupName + ".Orders";
    }

    // Daily failed-orders email report settings
    public static class DailyReport
    {
        public const string Default = GroupName + ".DailyReport";
    }

    // Default Talabat -> Foodics customer mapping per vendor
    public static class CustomerMapping
    {
        public const string Default = GroupName + ".CustomerMapping";
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
        // Bypass branch scoping entirely (see every branch). Grant to admin-type roles.
        public const string All = GroupName + ".Branches.All";
    }
}
