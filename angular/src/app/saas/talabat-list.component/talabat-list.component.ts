import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';

// PrimeNG Imports
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { Select } from 'primeng/select';
import { Checkbox } from 'primeng/checkbox';
import { MessageService, ConfirmationService } from 'primeng/api';

// ABP & Services
import { TalabatAccountService } from '../../proxy/volo/abp/tenant-management/talabat-account.service';
import { TalabatAccountDto, CreateUpdateTalabatAccountDto } from '../../proxy/volo/abp/tenant-management/talabat/models';
import { FoodicsService } from '../../proxy/foodics/foodics.service';
import { FoodicsAccountDto } from '../../proxy/foodics/models';
import { MenuSyncService } from '../../proxy/background-jobs/menu-sync.service';
import { FoodicsGroupWithProductCountDto } from '../../proxy/background-jobs/models';
import { LocalizationModule, LocalizationService } from '@abp/ng.core';

/**
 * Talabat Account Management Component
 * UPDATED: Now supports linking TalabatAccount with FoodicsAccount for multi-tenant sync
 * 
 * This component allows:
 * - Creating/editing/deleting TalabatAccount entities
 * - Linking TalabatAccount to FoodicsAccount (for automated menu sync)
 * - Managing credentials (username, password) per vendor
 * - Configuring platform settings (platformKey, platformRestaurantId)
 */
@Component({
  selector: 'app-talabat-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    ToastModule,
    TagModule,
    SkeletonModule,
    TooltipModule,
    DialogModule,
    PasswordModule,
    MessageModule,
    ConfirmDialogModule,
    Select,
    Checkbox,
    LocalizationModule
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './talabat-list.component.html',
  styleUrl: './talabat-list.component.scss'
})
export class TalabatListComponent implements OnInit {
  private talabatAccountService: TalabatAccountService = inject(TalabatAccountService);
  private foodicsService = inject(FoodicsService);
  private menuSyncService = inject(MenuSyncService);
  private messageService = inject(MessageService);
  private confirmationService = inject(ConfirmationService);
  private fb = inject(FormBuilder);
  private localization = inject(LocalizationService);

  // Table data
  accounts: TalabatAccountDto[] = [];
  foodicsAccounts: FoodicsAccountDto[] = [];
  foodicsGroups: FoodicsGroupWithProductCountDto[] = [];
  groupsLoading: boolean = false;
  totalRecords: number = 0;
  loading: boolean = false;

  // Pagination & Sorting
  rows: number = 10;
  first: number = 0;

  // Dialog & Form
  displayDialog: boolean = false;
  isEditMode: boolean = false;
  selectedAccountId?: string;
  accountForm!: FormGroup;
  submitting: boolean = false;

  // Platform Key options
  platformKeyOptions = [
    { label: 'TB (Global)', value: 'TB' },
    { label: 'TB_KW (Kuwait)', value: 'TB_KW' },
    { label: 'TB_AE (UAE)', value: 'TB_AE' },
    { label: 'TB_BH (Bahrain)', value: 'TB_BH' },
    { label: 'TB_OM (Oman)', value: 'TB_OM' },
    { label: 'TB_QA (Qatar)', value: 'TB_QA' },
    { label: 'TB_SA (Saudi Arabia)', value: 'TB_SA' },
    { label: 'TB_JO (Jordan)', value: 'TB_JO' },
    { label: 'TB_EG (Egypt)', value: 'TB_EG' }
  ];

  ngOnInit(): void {
    this.initializeForm();
    this.loadFoodicsAccounts();
  }

  /**
   * Initializes the account form with validators
   */
  private initializeForm(): void {
    this.accountForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      vendorCode: ['', [Validators.required, Validators.maxLength(50)]],
      chainCode: ['tlbt-pick', [Validators.maxLength(50)]],
      isActive: [true],
      userName: ['', [Validators.maxLength(100)]],
      password: ['', [Validators.maxLength(100)]],
      platformKey: ['TB', [Validators.maxLength(50)]],
      platformRestaurantId: ['', [Validators.required, Validators.maxLength(100)]],
      foodicsAccountId: [null],
      foodicsGroupId: [null],
      foodicsGroupName: [null]
    });

    // When Foodics account changes: load its groups and reset selection
    this.accountForm.get('foodicsAccountId')?.valueChanges.subscribe((foodicsAccountId: string | null) => {
      this.foodicsGroups = [];
      this.accountForm.patchValue({ foodicsGroupId: null, foodicsGroupName: null }, { emitEvent: false });

      if (!foodicsAccountId) {
        return;
      }

      this.loadFoodicsGroups(foodicsAccountId);
    });
  }

  loadFoodicsGroups(foodicsAccountId: string): void {
    this.groupsLoading = true;

    this.menuSyncService.getGroupsForAccount(foodicsAccountId).subscribe({
      next: (groups) => {
        this.foodicsGroups = groups || [];
        this.groupsLoading = false;
      },
      error: (error) => {
        console.error('Error loading Foodics groups:', error);
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to load Foodics groups'
        });
        this.foodicsGroups = [];
        this.groupsLoading = false;
      }
    });
  }

  onFoodicsGroupChange(groupId: string | null): void {
    if (!groupId) {
      this.accountForm.patchValue({ foodicsGroupId: null, foodicsGroupName: null }, { emitEvent: false });
      return;
    }

    const selected = this.foodicsGroups.find(g => (g.id || '').toLowerCase() === groupId.toLowerCase());
    const name = selected?.name || selected?.nameLocalized || groupId;
    this.accountForm.patchValue({ foodicsGroupId: groupId, foodicsGroupName: name }, { emitEvent: false });
  }

  /**
   * Loads accounts with pagination, sorting, and filtering (PrimeNG lazy load)
   */
  loadAccounts(event?: TableLazyLoadEvent): void {
    this.loading = true;

    // Calculate pagination
    const skipCount = event?.first ?? 0;
    const maxResultCount = event?.rows ?? this.rows;

    // Build sorting string (ABP format)
    let sorting = '';
    if (event?.sortField) {
      const sortField = event.sortField as string;
      const sortOrder = event.sortOrder === 1 ? 'asc' : 'desc';
      sorting = `${sortField} ${sortOrder}`;
    }

    this.talabatAccountService.getList({
      skipCount,
      maxResultCount,
      sorting
    }).subscribe({
      next: (response) => {
        this.accounts = response.items || [];
        this.totalRecords = response.totalCount || 0;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading Talabat accounts:', error);
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to load Talabat accounts'
        });
        this.loading = false;
      }
    });
  }

  /**
   * Loads Foodics accounts for dropdown (for linking)
   */
  loadFoodicsAccounts(): void {
    this.foodicsService.getList({
      maxResultCount: 100,
      skipCount: 0
    }).subscribe({
      next: (response) => {
        this.foodicsAccounts = response.items || [];
      },
      error: (error) => {
        console.error('Error loading Foodics accounts:', error);
      }
    });
  }

  /**
   * Opens create dialog
   */
  createAccount(): void {
    this.isEditMode = false;
    this.selectedAccountId = undefined;
    this.foodicsGroups = [];
    this.accountForm.reset({
      isActive: true,
      platformKey: 'TB',
      chainCode: 'tlbt-pick',
      foodicsGroupId: null,
      foodicsGroupName: null
    });
    this.displayDialog = true;
  }

  /**
   * Opens edit dialog
   */
  editAccount(account: TalabatAccountDto): void {
    this.isEditMode = true;
    this.selectedAccountId = account.id;

    this.accountForm.patchValue({
      name: account.name,
      vendorCode: account.vendorCode,
      chainCode: account.chainCode,
      isActive: account.isActive,
      userName: account.userName,
      platformKey: account.platformKey || 'TB',
      platformRestaurantId: account.platformRestaurantId,
      foodicsAccountId: account.foodicsAccountId,
      foodicsGroupId: account.foodicsGroupId ?? null,
      foodicsGroupName: account.foodicsGroupName ?? null
    });

    // Load groups for the selected Foodics account (so the dropdown is populated in edit mode)
    if (account.foodicsAccountId) {
      this.loadFoodicsGroups(account.foodicsAccountId);
    } else {
      this.foodicsGroups = [];
    }

    this.displayDialog = true;
  }

  /**
   * Saves (creates or updates) Talabat account
   */
  saveAccount(): void {
    if (this.accountForm.invalid) {
      this.markFormGroupTouched(this.accountForm);
      return;
    }

    this.submitting = true;
    const formValue = this.accountForm.value;
    const password = (formValue.password || '').trim();
    const dto: CreateUpdateTalabatAccountDto = {
      name: formValue.name,
      vendorCode: formValue.vendorCode,
      chainCode: formValue.chainCode,
      isActive: formValue.isActive,
      userName: formValue.userName,
      password: password ? password : undefined,
      platformKey: formValue.platformKey,
      platformRestaurantId: formValue.platformRestaurantId,
      foodicsAccountId: formValue.foodicsAccountId,
      foodicsGroupId: formValue.foodicsGroupId ?? undefined,
      foodicsGroupName: formValue.foodicsGroupName ?? undefined,
      syncAllBranches: true // Default to true since branch selection is removed
    };

    const request = this.isEditMode && this.selectedAccountId
      ? this.talabatAccountService.update(this.selectedAccountId, dto)
      : this.talabatAccountService.create(dto);

    request.subscribe({
      next: () => {
        this.submitting = false;
        this.displayDialog = false;
        this.messageService.add({
          severity: 'success',
          summary: 'Success',
          detail: this.isEditMode 
            ? 'Talabat account updated successfully' 
            : 'Talabat account created successfully'
        });
        this.loadAccounts();
      },
      error: (error) => {
        console.error('Error saving Talabat account:', error);
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: error?.error?.error?.message || 'Failed to save Talabat account'
        });
        this.submitting = false;
      }
    });
  }

  /**
   * Deletes Talabat account with confirmation
   */
  deleteAccount(account: TalabatAccountDto): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to delete "${account.name}" (${account.vendorCode})?`,
      header: 'Delete Confirmation',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.talabatAccountService.delete(account.id).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: 'Success',
              detail: 'Talabat account deleted successfully'
            });
            this.loadAccounts();
          },
          error: (error) => {
            console.error('Error deleting Talabat account:', error);
            this.messageService.add({
              severity: 'error',
              summary: 'Error',
              detail: 'Failed to delete Talabat account'
            });
          }
        });
      }
    });
  }

  /**
   * Helper to get Foodics account display name
   */
  getFoodicsAccountDisplay(foodicsAccountId: string | null | undefined): string {
    if (!foodicsAccountId) {
      return 'Not linked';
    }
    const account = this.foodicsAccounts.find(a => a.id === foodicsAccountId);
    return account?.brandName || account?.oAuthClientId || foodicsAccountId;
  }

  /**
   * Marks all form controls as touched to trigger validation messages
   */
  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(key => {
      const control = formGroup.get(key);
      control?.markAsTouched();
    });
  }

  /**
   * Cancels dialog
   */
  cancelDialog(): void {
    this.displayDialog = false;
    this.accountForm.reset();
  }
}
