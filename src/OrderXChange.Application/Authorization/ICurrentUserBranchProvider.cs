using System.Threading.Tasks;

namespace OrderXChange.Authorization;

/// <summary>
/// Resolves the branch scope of the currently authenticated user (union of the branches
/// granted to their roles, or "all branches" for admin-type users). Feature app services
/// call this to enforce branch-scoped authorization on their queries.
/// </summary>
public interface ICurrentUserBranchProvider
{
    Task<UserBranchScope> GetScopeAsync();
}
