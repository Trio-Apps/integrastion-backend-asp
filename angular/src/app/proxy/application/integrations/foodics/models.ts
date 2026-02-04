
export interface FoodicsAccountSummaryDto {
  totalBranches: number;
  totalProducts: number;
  totalCategories: number;
  totalMenuGroups: number;
  activeProducts: number;
  inactiveProducts: number;
  allCategoryIds: string[];
  allMenuGroupIds: string[];
}

export interface FoodicsAggregatedCategoryDto {
  category: FoodicsCategoryInfoDto;
  children: FoodicsAggregatedChildDto[];
}

export interface FoodicsAggregatedChildDto {
  type?: string;
  id?: string;
  product: FoodicsProductDetailDto;
}

export interface FoodicsAggregatedCustomGroupDto {
  groupId?: string;
  children: FoodicsAggregatedChildDto[];
}

export interface FoodicsAggregatedMenuDto {
  categories: FoodicsAggregatedCategoryDto[];
  custom: FoodicsAggregatedCustomGroupDto[];
}

export interface FoodicsBranchAnalysisDto {
  branch: FoodicsBranchDto;
  stats: FoodicsBranchStatsDto;
  categories: FoodicsBranchCategoryDto[];
  menuGroups: FoodicsBranchMenuGroupDto[];
  productsByCategory: FoodicsAggregatedCategoryDto[];
}

export interface FoodicsBranchCategoryDto {
  category: FoodicsCategoryInfoDto;
  productCount: number;
  activeProductCount: number;
  productIds: string[];
}

export interface FoodicsBranchDto {
  id?: string;
  name?: string;
  name_localized?: string;
  timezone?: string;
  is_open?: boolean;
  is_active?: boolean;
}

export interface FoodicsBranchMenuGroupDto {
  groupId?: string;
  groupName?: string;
  productCount: number;
  activeProductCount: number;
  productIds: string[];
  categoryIds: string[];
}

export interface FoodicsBranchStatsDto {
  totalProducts: number;
  activeProducts: number;
  inactiveProducts: number;
  categoriesCount: number;
  menuGroupsCount: number;
  productsWithModifiers: number;
  productsWithoutCategory: number;
  productsWithoutMenuGroup: number;
}

export interface FoodicsCategoryInfoDto {
  id?: string;
  name?: string;
  name_localized?: string;
}

export interface FoodicsDiscountDto {
  id?: string;
  name?: string;
  discount_type?: string;
  discount_value?: number;
}

export interface FoodicsEnhancedAggregatedMenuDto {
  accountSummary: FoodicsAccountSummaryDto;
  branchAnalysis: FoodicsBranchAnalysisDto[];
  categories: FoodicsAggregatedCategoryDto[];
  custom: FoodicsAggregatedCustomGroupDto[];
}

export interface FoodicsGroupInfoDto {
  id?: string;
  name?: string;
  name_localized?: string;
}

export interface FoodicsIngredientDto {
  id?: string;
  name?: string;
  branches: FoodicsBranchDto[];
}

export interface FoodicsMenuDisplayCategoryDto {
  category_id?: string;
  children: FoodicsMenuDisplayChildDto[];
}

export interface FoodicsMenuDisplayChildDto {
  child_type?: string;
  child_id?: string;
  children: FoodicsMenuDisplayChildDto[];
}

export interface FoodicsMenuDisplayDataDto {
  categories: FoodicsMenuDisplayCategoryDto[];
  custom: FoodicsMenuDisplayGroupDto[];
}

export interface FoodicsMenuDisplayGroupDto {
  group_id?: string;
  children: FoodicsMenuDisplayChildDto[];
}

export interface FoodicsMenuDisplayResponseDto {
  data: FoodicsMenuDisplayDataDto;
}

export interface FoodicsModifierDto {
  id?: string;
  name?: string;
  name_localized?: string;
  min_allowed?: number;
  max_allowed?: number;
  options: FoodicsModifierOptionDto[];
}

export interface FoodicsModifierOptionDto {
  id?: string;
  name?: string;
  name_localized?: string;
  price?: number;
  image?: string;
  branches: FoodicsBranchDto[];
}

export interface FoodicsPriceTagDto {
  id?: string;
  name?: string;
  name_localized?: string;
  price?: number;
}

export interface FoodicsProductDetailDto extends FoodicsProductInfoDto {
  description?: string;
  description_localized?: string;
  image?: string;
  is_active?: boolean;
  sku?: string;
  barcode?: string;
  category_id?: string;
  tax_group_id?: string;
  category: FoodicsCategoryInfoDto;
  price_tags: FoodicsPriceTagDto[];
  tax_group: FoodicsTaxGroupDto;
  tags: FoodicsTagDto[];
  branches: FoodicsBranchDto[];
  ingredients: FoodicsIngredientDto[];
  modifiers: FoodicsModifierDto[];
  discounts: FoodicsDiscountDto[];
  timed_events: FoodicsTimedEventDto[];
  groups: FoodicsGroupInfoDto[];
  deleted_at?: string;
}

export interface FoodicsProductInfoDto {
  id?: string;
  name?: string;
  name_localized?: string;
  price?: number;
}

export interface FoodicsTagDto {
  id?: string;
  name?: string;
  name_localized?: string;
}

export interface FoodicsTaxGroupDto {
  id?: string;
  name?: string;
  name_localized?: string;
  rate?: number;
}

export interface FoodicsTimedEventDto {
  id?: string;
  name?: string;
  start_time?: string;
  end_time?: string;
}

export interface GetEnhancedAggregatedMenuRequest {
  branchId?: string;
  foodicsAccountId?: string;
  includeProductDetails: boolean;
  includeInactiveProducts: boolean;
  includeUncategorizedProducts: boolean;
}

export interface ProductAvailabilitySyncResultDto {
  totalProducts: number;
  totalBranches: number;
  availableProducts: number;
  unavailableProducts: number;
  products: TalabatProductAvailabilityDto[];
}

export interface TalabatProductAvailabilityDto {
  productId?: string;
  productName?: string;
  productSku?: string;
  branchId?: string;
  branchName?: string;
  branchReference?: string;
  price?: number;
  isAvailable: boolean;
  isActive: boolean;
  priceTagIds: string[];
  priceTagNames: string[];
}
