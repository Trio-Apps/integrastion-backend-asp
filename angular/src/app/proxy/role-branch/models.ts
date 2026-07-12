export interface RoleBranchDto {
  foodicsAccountId: string;
  foodicsBranchId: string;
  foodicsBranchName?: string;
}

export interface UpdateRoleBranchesDto {
  branches: RoleBranchDto[];
}
