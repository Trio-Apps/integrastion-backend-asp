using System.Collections.Generic;

namespace OrderXChange.Authorization;

/// <summary>
/// The set of Foodics branches the current user may access. Feature queries use this to
/// filter data (orders, availability, …) to the user's branches.
/// </summary>
public class UserBranchScope
{
    /// <summary>When true the user may access every branch (no filtering is applied).</summary>
    public bool AllBranches { get; }

    /// <summary>
    /// The explicit set of allowed Foodics branch ids. Only meaningful when
    /// <see cref="AllBranches"/> is false. An empty set here means "no branches" (fail-closed).
    /// </summary>
    public IReadOnlySet<string> BranchIds { get; }

    private UserBranchScope(bool allBranches, IReadOnlySet<string> branchIds)
    {
        AllBranches = allBranches;
        BranchIds = branchIds;
    }

    public static UserBranchScope All() => new(true, new HashSet<string>());

    public static UserBranchScope Restricted(HashSet<string> branchIds) => new(false, branchIds);

    /// <summary>True if the given Foodics branch is accessible under this scope.</summary>
    public bool CanAccess(string? foodicsBranchId)
        => AllBranches || (!string.IsNullOrEmpty(foodicsBranchId) && BranchIds.Contains(foodicsBranchId));
}
