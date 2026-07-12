using System;
using System.Linq;
using System.Threading.Tasks;
using Foodics;
using Moq;
using OrderXChange.Authorization;
using OrderXChange.Domain.Staging;
using OrderXChange.Permissions;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.TenantManagement.Talabat;
using Xunit;

namespace OrderXChange.Search;

/// <summary>
/// Verifies the unified Smart Search (BRS §4): a single keyword fanned out across orders,
/// catalog items &amp; modifiers, and branches — grouped, branch-scoped and permission-trimmed.
///
/// The three data-access dependencies use the REAL repositories (SQLite), so the actual EF
/// query is exercised. The branch scope and permission checks are supplied as controlled fakes
/// because the integration test host runs unauthenticated with an always-allow permission
/// checker, which would otherwise make scope/permission behavior untestable.
/// </summary>
public abstract class SmartSearchAppServiceTests<TStartupModule> : OrderXChangeApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string BranchA = "branch-A";
    private const string BranchB = "branch-B";
    private const string VendorA = "VEND-A";
    private const string VendorB = "VEND-B";

    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderRepo;
    private readonly IRepository<FoodicsProductStaging, Guid> _productRepo;
    private readonly IRepository<TalabatAccount, Guid> _accountRepo;
    private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepo;

    protected SmartSearchAppServiceTests()
    {
        _orderRepo = GetRequiredService<IRepository<TalabatOrderSyncLog, Guid>>();
        _productRepo = GetRequiredService<IRepository<FoodicsProductStaging, Guid>>();
        _accountRepo = GetRequiredService<IRepository<TalabatAccount, Guid>>();
        _foodicsAccountRepo = GetRequiredService<IRepository<FoodicsAccount, Guid>>();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Groups_matches_across_orders_items_and_branches()
    {
        await SeedAsync();

        var result = await SearchAsync("Nova", UserBranchScope.All());

        // Orders: both customers carry the keyword in their name.
        result.Orders.Count.ShouldBe(2);
        result.Orders.ShouldAllBe(o => o.CustomerName!.Contains("Nova"));

        // Items: one matches by product name, the other by a modifier option name.
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(i => i.Name == "Nova Burger");
        var byModifier = result.Items.Single(i => i.Name == "Veggie Wrap");
        byModifier.MatchedModifiers.ShouldContain("Nova Sauce");

        // Branches: only the "Nova Downtown" branch name matches.
        result.Branches.Count.ShouldBe(1);
        result.Branches.Single().BranchName.ShouldBe("Nova Downtown");

        result.TotalCount.ShouldBe(result.Orders.Count + result.Items.Count + result.Branches.Count);
    }

    [Fact]
    public async Task Restricts_every_group_to_the_users_branch_scope()
    {
        await SeedAsync();

        var scope = UserBranchScope.Restricted(new(StringComparer.OrdinalIgnoreCase) { BranchA });

        var result = await SearchAsync("Nova", scope);

        // Only branch-A's vendor order, product and branch survive; branch-B's are excluded.
        result.Orders.Select(o => o.VendorCode).ShouldBe([VendorA]);
        result.Items.Select(i => i.Name).ShouldBe(["Nova Burger"]);
        result.Branches.Select(b => b.BranchId).ShouldBe([BranchA]);
    }

    [Fact]
    public async Task Fails_closed_when_the_user_has_no_branch_grants()
    {
        await SeedAsync();

        var scope = UserBranchScope.Restricted(new(StringComparer.OrdinalIgnoreCase)); // no branches

        var result = await SearchAsync("Nova", scope);

        result.Orders.ShouldBeEmpty();
        result.Items.ShouldBeEmpty();
        result.Branches.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Trims_the_orders_group_without_the_Orders_permission()
    {
        await SeedAsync();

        var result = await SearchAsync("Nova", UserBranchScope.All(), canOrders: false, canItems: true);

        result.Orders.ShouldBeEmpty();
        result.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Trims_the_items_group_without_the_Availability_permission()
    {
        await SeedAsync();

        var result = await SearchAsync("Nova", UserBranchScope.All(), canOrders: true, canItems: false);

        result.Items.ShouldBeEmpty();
        result.Orders.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Returns_nothing_with_neither_feature_permission()
    {
        await SeedAsync();

        var result = await SearchAsync("Nova", UserBranchScope.All(), canOrders: false, canItems: false);

        result.Orders.ShouldBeEmpty();
        result.Items.ShouldBeEmpty();
        result.Branches.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Blank_keyword_yields_an_empty_result()
    {
        await SeedAsync();

        var result = await SearchAsync("   ", UserBranchScope.All());

        result.Keyword.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    // ── Harness ────────────────────────────────────────────────────────────────

    private Task<SmartSearchResultDto> SearchAsync(
        string keyword, UserBranchScope scope, bool canOrders = true, bool canItems = true)
    {
        return WithUnitOfWorkAsync(() =>
        {
            var service = BuildService(scope, canOrders, canItems);
            return service.SearchAsync(new SmartSearchInput { Keyword = keyword });
        });
    }

    /// <summary>
    /// Builds the service under test with REAL repositories but controlled branch scope and
    /// permission checks (see class remarks for why these two are faked).
    /// </summary>
    private SmartSearchAppService BuildService(UserBranchScope scope, bool canOrders, bool canItems)
    {
        var branchProvider = new Mock<ICurrentUserBranchProvider>();
        branchProvider.Setup(p => p.GetScopeAsync()).ReturnsAsync(scope);

        var permissionChecker = new Mock<IPermissionChecker>();
        permissionChecker
            .Setup(p => p.IsGrantedAsync(OrderXChangePermissions.Orders.Default))
            .ReturnsAsync(canOrders);
        permissionChecker
            .Setup(p => p.IsGrantedAsync(OrderXChangePermissions.Availability.Default))
            .ReturnsAsync(canItems);

        return new SmartSearchAppService(
            _orderRepo, _productRepo, _accountRepo, branchProvider.Object, permissionChecker.Object);
    }

    private Task SeedAsync()
    {
        return WithUnitOfWorkAsync(async () =>
        {
            // Parent FoodicsAccount rows first — TalabatAccount, orders and products all carry a
            // foreign key to FoodicsAccount, which SQLite enforces. Ids are generated on insert,
            // so capture them and wire the dependent rows to the real ids.
            var accountA = await _foodicsAccountRepo.InsertAsync(NewFoodicsAccount("Pick Branch A"), autoSave: true);
            var accountB = await _foodicsAccountRepo.InsertAsync(NewFoodicsAccount("Pick Branch B"), autoSave: true);

            // Two vendor accounts, one per branch. Only branch-A's name carries the keyword.
            await _accountRepo.InsertAsync(NewAccount(accountA.Id, VendorA, BranchA, "Nova Downtown"), autoSave: true);
            await _accountRepo.InsertAsync(NewAccount(accountB.Id, VendorB, BranchB, "Airport Terminal"), autoSave: true);

            // One order per branch; both customers carry the keyword.
            await _orderRepo.InsertAsync(NewOrder(accountA.Id, VendorA, "Nova Customer"), autoSave: true);
            await _orderRepo.InsertAsync(NewOrder(accountB.Id, VendorB, "Nova Buyer"), autoSave: true);

            // One product per account: A matches by name, B matches by a modifier option name.
            await _productRepo.InsertAsync(
                NewProduct(accountA.Id, BranchA, "Nova Burger", "[{\"name\":\"Cheese\"}]"), autoSave: true);
            await _productRepo.InsertAsync(
                NewProduct(accountB.Id, BranchB, "Veggie Wrap", "[{\"name\":\"Nova Sauce\"}]"), autoSave: true);
        });
    }

    private static FoodicsAccount NewFoodicsAccount(string brandName)
        => new()
        {
            BrandName = brandName,
            OAuthClientId = "client",
            OAuthClientSecret = "secret",
            AccessToken = "token"
        };

    private static TalabatAccount NewAccount(Guid foodicsAccountId, string vendorCode, string branchId, string branchName)
        => new()
        {
            Name = branchName,
            VendorCode = vendorCode,
            ChainCode = "CHAIN",
            ApiKey = "key",
            ApiSecret = "secret",
            FoodicsAccountId = foodicsAccountId,
            FoodicsBranchId = branchId,
            FoodicsBranchName = branchName
        };

    private static TalabatOrderSyncLog NewOrder(Guid foodicsAccountId, string vendorCode, string customerName)
        => new()
        {
            FoodicsAccountId = foodicsAccountId,
            VendorCode = vendorCode,
            CustomerName = customerName,
            Status = "Received",
            ReceivedAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)
        };

    private static FoodicsProductStaging NewProduct(Guid foodicsAccountId, string branchId, string name, string modifiersJson)
        => new()
        {
            FoodicsAccountId = foodicsAccountId,
            FoodicsProductId = Guid.NewGuid().ToString("N"),
            Name = name,
            BranchId = branchId,
            ModifiersJson = modifiersJson,
            IsActive = true
        };
}
