using System.Collections.Generic;

namespace OrderXChange.Authorization;

/// <summary>Replace-set of the branches a role may access.</summary>
public class UpdateRoleBranchesDto
{
    public List<RoleBranchDto> Branches { get; set; } = new();
}
