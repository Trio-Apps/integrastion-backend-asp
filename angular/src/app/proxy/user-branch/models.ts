export interface UserBranchDto {
  foodicsAccountId?: string;
  foodicsBranchId?: string;
  foodicsBranchName?: string;
}

export interface UpdateUserBranchesDto {
  branches: UserBranchDto[];
}
