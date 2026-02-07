import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { CheckboxModule } from 'primeng/checkbox';
import { LocalizationPipe, MultiTenancyService, SessionStateService } from '@abp/ng.core';
import { MenuSyncService } from '@proxy/background-jobs';
import type { FoodicsEnhancedAggregatedMenuDto, FoodicsAggregatedCategoryDto, FoodicsProductDetailDto } from '@proxy/application/integrations/foodics/models';
import { TenantService } from '../proxy/volo/abp/tenant-management/tenant.service';
import type { GetTenantsInput, TenantDto } from '../proxy/volo/abp/tenant-management/models';

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    TableModule, 
    ButtonModule,
    CardModule,
    TagModule,
    CheckboxModule,
    LocalizationPipe
  ],
  templateUrl: './menu-demo.component.html'
})
export class MenuDemoComponent implements OnInit {

  private menuSync = inject(MenuSyncService);
  private tenantService = inject(TenantService);
  private multiTenancy = inject(MultiTenancyService);
  private sessionState = inject(SessionStateService);

  categories: CategoryView[] = [];
  selectedCategory: CategoryView | null = null;
  selectedProducts: FoodicsProductDetailDto[] = [];
  showInactiveProducts = false;

  // UI state
  error = '';
  isLoading = false;

  tenants: TenantDto[] = [];
  canSwitchTenant = false;
  selectedTenantName: string | null = null;

  ngOnInit(): void {
    this.loadTenants();
    this.loadCategories();
  }

  loadTenants(): void {
    const input: GetTenantsInput = {
      skipCount: 0,
      maxResultCount: 1000,
      sorting: 'name'
    };

    this.tenantService.getList(input).subscribe({
      next: result => {
        this.tenants = result.items ?? [];
        this.canSwitchTenant = true;
      },
      error: () => {
        this.tenants = [];
        this.canSwitchTenant = false;
      }
    });
  }

  applyTenantSelection(): void {
    const tenantName = (this.selectedTenantName ?? '').trim();
    if (!tenantName) {
      this.sessionState.setTenant(null);
      window.location.reload();
      return;
    }

    this.multiTenancy.setTenantByName(tenantName).subscribe({
      next: () => window.location.reload(),
      error: () => {
        this.error = 'Failed to switch tenant.';
      }
    });
  }

  clearTenantSelection(): void {
    this.selectedTenantName = null;
    this.sessionState.setTenant(null);
    window.location.reload();
  }

  loadCategories(): void {
    this.error = '';
    this.isLoading = true;
    
    this.menuSync.getEnhancedAggregated({
      foodicsAccountId: undefined,
      branchId: undefined,
      includeProductDetails: true,
      includeInactiveProducts: true,
      includeUncategorizedProducts: false
    }).subscribe({
      next: result => {
        this.categories = this.mapCategories(result);
        this.error = '';
        this.selectedCategory = null;
        this.selectedProducts = [];
      },
      error: err => {
        this.error = this.formatHttpError(err);
        this.categories = [];
        this.selectedCategory = null;
        this.selectedProducts = [];
      }
    }).add(() => {
      this.isLoading = false;
    });
  }

  onToggleInactive(): void {
    this.categories = this.categories.map(c => this.recalculateCategory(c));
    if (this.selectedCategory) {
      const updated = this.categories.find(c => c.id === this.selectedCategory?.id);
      if (updated) {
        this.selectCategory(updated);
      }
    }
  }

  selectCategory(category: CategoryView): void {
    this.selectedCategory = category;
    this.selectedProducts = this.filterProducts(category.products);
  }

  private mapCategories(result: FoodicsEnhancedAggregatedMenuDto): CategoryView[] {
    const categories = result.categories ?? [];
    return categories
      .map(c => this.buildCategoryView(c))
      .filter(c => c.totalProducts > 0)
      .sort((a, b) => b.totalProducts - a.totalProducts || a.name.localeCompare(b.name));
  }

  private buildCategoryView(category: FoodicsAggregatedCategoryDto): CategoryView {
    const rawProducts = (category.children ?? [])
      .map(child => child.product)
      .filter(Boolean);

    const activeProducts = rawProducts.filter(p => p.is_active === true);
    const inactiveProducts = rawProducts.filter(p => p.is_active !== true);
    const visibleProducts = this.filterProducts(rawProducts);

    return {
      id: category.category?.id ?? '',
      name: category.category?.name || category.category?.name_localized || category.category?.id || 'Unknown',
      totalProducts: visibleProducts.length,
      activeProducts: activeProducts.length,
      inactiveProducts: inactiveProducts.length,
      products: rawProducts
    };
  }

  private recalculateCategory(category: CategoryView): CategoryView {
    const activeProducts = category.products.filter(p => p.is_active === true);
    const inactiveProducts = category.products.filter(p => p.is_active !== true);
    const visibleProducts = this.filterProducts(category.products);

    return {
      ...category,
      totalProducts: visibleProducts.length,
      activeProducts: activeProducts.length,
      inactiveProducts: inactiveProducts.length
    };
  }

  private filterProducts(products: FoodicsProductDetailDto[]): FoodicsProductDetailDto[] {
    if (this.showInactiveProducts) {
      return products;
    }
    return products.filter(p => p.is_active === true);
  }

  private formatHttpError(err: any): string {
    try {
      if (err?.error) {
        return JSON.stringify(err.error, null, 2);
      }
      return err?.message ?? String(err);
    } catch {
      return String(err);
    }
  }
}

interface CategoryView {
  id: string;
  name: string;
  totalProducts: number;
  activeProducts: number;
  inactiveProducts: number;
  products: FoodicsProductDetailDto[];
}


