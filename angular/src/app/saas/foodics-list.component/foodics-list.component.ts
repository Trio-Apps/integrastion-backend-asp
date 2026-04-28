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
import { MessageService, ConfirmationService } from 'primeng/api';

// ABP & Services
import { FoodicsService } from '../../proxy/foodics/foodics.service';
import { FoodicsAccountDto, CreateUpdateFoodicsAccountDto, FoodicsConnectionTestResultDto } from '../../proxy/foodics/models';
import { LocalizationModule, LocalizationService, PagedResultDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

@Component({
  selector: 'app-foodics-list',
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
    LocalizationModule
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './foodics-list.component.html',
  styleUrl: './foodics-list.component.scss'
})
export class FoodicsListComponent implements OnInit {
  private foodicsService = inject(FoodicsService);
  private messageService = inject(MessageService);
  private confirmationService = inject(ConfirmationService);
  private fb = inject(FormBuilder);
  private localization = inject(LocalizationService);

  // Table data
  accounts: FoodicsAccountDto[] = [];
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
  testingAccountId?: string;
  testResult?: FoodicsConnectionTestResultDto;
  displayTestDialog: boolean = false;
  readonly environmentOptions = [
    { label: this.l('::Foodics.Environment.Sandbox'), value: 'Sandbox' },
    { label: this.l('::Foodics.Environment.Production'), value: 'Production' }
  ];

  ngOnInit(): void {
    // Initial load is handled by lazy loading event
    this.initializeForm();
  }

  /**
   * Initializes the account form with validators
   */
  private initializeForm(): void {
    this.accountForm = this.fb.group({
      brandName: ['', [Validators.required]],
      oAuthClientId: ['', [Validators.required]],
      oAuthClientSecret: ['', [Validators.required]],
      accessToken: [''],
      apiEnvironment: ['Sandbox', [Validators.required]]
    });
  }

  /**
   * Loads accounts with pagination, sorting, and filtering
   */
  loadAccounts(event?: TableLazyLoadEvent): void {
    this.loading = true;

    // Calculate pagination
    const skipCount = event?.first ?? 0;
    const maxResultCount = event?.rows ?? this.rows;

    // Build sorting string
    let sorting = '';
    if (event?.sortField) {
      sorting = `${event.sortField} ${event.sortOrder === 1 ? 'asc' : 'desc'}`;
    }

    // Build input parameters
    const input: PagedAndSortedResultRequestDto = {
      skipCount,
      maxResultCount,
      sorting: sorting || undefined
    };

    // Fetch data from API
    this.foodicsService.getList(input).subscribe({
      next: (result: PagedResultDto<FoodicsAccountDto>) => {
        this.accounts = result.items || [];
        this.totalRecords = result.totalCount || 0;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading accounts:', error);
        this.messageService.add({
          severity: 'error',
          summary: this.l('::Common.Error'),
          detail: this.l('::Foodics.Toast.LoadError'),
          life: 3000
        });
        this.loading = false;
        this.accounts = [];
        this.totalRecords = 0;
      }
    });
  }


  /**
   * Refreshes the table
   */
  refresh(): void {
    this.loadAccounts({ first: this.first, rows: this.rows });
  }

  /**
   * Opens the create dialog
   */
  openCreateDialog(): void {
    this.isEditMode = false;
    this.selectedAccountId = undefined;
    this.displayDialog = true;
    this.accountForm.reset();
    this.accountForm.patchValue({ apiEnvironment: 'Sandbox' });
  }

  /**
   * Opens the edit dialog
   */
  openEditDialog(account: FoodicsAccountDto): void {
    this.isEditMode = true;
    this.selectedAccountId = account.id;
    this.displayDialog = true;
    
    // Populate form with existing data
    this.accountForm.patchValue({
      brandName: account.brandName || '',
      oAuthClientId: account.oAuthClientId || '',
      oAuthClientSecret: account.oAuthClientSecret || '',
      accessToken: account.accessToken || '',
      apiEnvironment: account.apiEnvironment || 'Sandbox'
    });
  }

  /**
   * Closes the dialog
   */
  closeDialog(): void {
    this.displayDialog = false;
    this.accountForm.reset();
    this.submitting = false;
    this.isEditMode = false;
    this.selectedAccountId = undefined;
  }

  /**
   * Saves the account (create or update)
   */
  saveAccount(): void {
    // Validate form
    if (this.accountForm.invalid) {
      Object.keys(this.accountForm.controls).forEach(key => {
        this.accountForm.controls[key].markAsTouched();
      });
      return;
    }

    this.submitting = true;

    // Prepare DTO
    const dto: CreateUpdateFoodicsAccountDto = {
      brandName: this.accountForm.value.brandName,
      oAuthClientId: this.accountForm.value.oAuthClientId,
      oAuthClientSecret: this.accountForm.value.oAuthClientSecret,
      accessToken: this.accountForm.value.accessToken || undefined,
      apiEnvironment: this.accountForm.value.apiEnvironment
    };

    if (this.isEditMode && this.selectedAccountId) {
      // Update existing account
      this.foodicsService.update(this.selectedAccountId, dto).subscribe({
        next: (result: FoodicsAccountDto) => {
          this.messageService.add({
            severity: 'success',
            summary: this.l('::Common.Success'),
            detail: this.l('::Foodics.Toast.UpdateSuccess', result.brandName || result.oAuthClientId || ''),
            life: 3000
          });
          this.submitting = false;
          this.closeDialog();
          this.refresh();
        },
        error: (error) => {
          console.error('Error updating account:', error);
          this.messageService.add({
            severity: 'error',
            summary: this.l('::Common.Error'),
            detail: error?.error?.error?.message || this.l('::Foodics.Toast.UpdateError'),
            life: 5000
          });
          this.submitting = false;
        }
      });
    } else {
      // Create new account
      this.foodicsService.create(dto).subscribe({
        next: (result: FoodicsAccountDto) => {
          this.messageService.add({
            severity: 'success',
            summary: this.l('::Common.Success'),
            detail: this.l('::Foodics.Toast.CreateSuccess', result.brandName || result.oAuthClientId || ''),
            life: 3000
          });
          this.submitting = false;
          this.closeDialog();
          this.refresh();
        },
        error: (error) => {
          console.error('Error creating account:', error);
          this.messageService.add({
            severity: 'error',
            summary: this.l('::Common.Error'),
            detail: error?.error?.error?.message || this.l('::Foodics.Toast.CreateError'),
            life: 5000
          });
          this.submitting = false;
        }
      });
    }
  }

  /**
   * Helper method to check if a form field has errors and is touched
   */
  isFieldInvalid(fieldName: string): boolean {
    const field = this.accountForm.get(fieldName);
    return !!(field && field.invalid && field.touched);
  }

  /**
   * Helper method to get error message for a field
   */
  getFieldError(fieldName: string): string {
    const field = this.accountForm.get(fieldName);
    if (field?.hasError('required')) {
      return this.l('::Validation.Required', this.getFieldLabel(fieldName));
    }
    return '';
  }

  /**
   * Helper method to get field label
   */
  private getFieldLabel(fieldName: string): string {
    const labels: { [key: string]: string } = {
      brandName: this.l('::Foodics.Dialog.BrandNameLabel'),
      oAuthClientId: this.l('::Foodics.Dialog.ClientIdLabel'),
      oAuthClientSecret: this.l('::Foodics.Dialog.ClientSecretLabel'),
      accessToken: this.l('::Foodics.Dialog.AccessTokenLabel'),
      apiEnvironment: this.l('::Foodics.Dialog.EnvironmentLabel')
    };
    return labels[fieldName] || fieldName;
  }

  /**
   * Confirms and deletes an account
   */
  deleteAccount(account: FoodicsAccountDto): void {
    const accountName = account.brandName || account.oAuthClientId || 'this account';
    this.confirmationService.confirm({
      message: this.l('::Foodics.Confirm.DeleteMessage', accountName),
      header: this.l('::Foodics.Confirm.Title'),
      icon: 'pi pi-exclamation-triangle',
      acceptIcon: 'pi pi-check',
      rejectIcon: 'pi pi-times',
      acceptLabel: this.l('::Common.YesDelete'),
      rejectLabel: this.l('::Common.Cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-secondary p-button-outlined',
      accept: () => {
        this.foodicsService.delete(account.id!).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: this.l('::Common.Deleted'),
              detail: this.l('::Foodics.Toast.DeleteSuccess', accountName),
              life: 3000
            });
            this.refresh();
          },
          error: (error) => {
            console.error('Error deleting account:', error);
            this.messageService.add({
              severity: 'error',
              summary: this.l('::Common.Error'),
              detail: error?.error?.error?.message || this.l('::Foodics.Toast.DeleteError'),
              life: 5000
            });
          }
        });
      }
    });
  }

  testConnection(account: FoodicsAccountDto): void {
    if (!account.id || this.testingAccountId) {
      return;
    }

    this.testingAccountId = account.id;
    this.testResult = undefined;

    this.foodicsService.testConnection(account.id).subscribe({
      next: result => {
        this.testResult = result;
        this.displayTestDialog = true;
        this.testingAccountId = undefined;

        this.messageService.add({
          severity: result.success ? 'success' : 'error',
          summary: result.success ? 'Connection succeeded' : 'Connection failed',
          detail: result.message || 'Foodics connection test completed.',
          life: result.success ? 3000 : 7000
        });

        if (result.success) {
          this.refresh();
        }
      },
      error: error => {
        console.error('Error testing Foodics connection:', error);
        this.testResult = {
          success: false,
          message: error?.error?.error?.message || error?.message || 'Connection test request failed.',
          details: JSON.stringify(error?.error || error, null, 2),
          apiEnvironment: account.apiEnvironment,
          accessTokenConfigured: !!account.accessToken,
          testedAtUtc: new Date().toISOString()
        };
        this.displayTestDialog = true;
        this.testingAccountId = undefined;
      }
    });
  }

  /**
   * Masks sensitive data for display
   */
  maskSecret(secret?: string): string {
    if (!secret || secret.length < 4) {
      return '••••••••';
    }
    return secret.substring(0, 4) + '••••••••';
  }

  private l(key: string, ...interpolateParams: string[]): string {
    return this.localization.instant(key, ...interpolateParams);
  }
}

