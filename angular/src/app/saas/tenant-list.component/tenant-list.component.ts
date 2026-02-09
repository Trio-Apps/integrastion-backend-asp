import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';

// PrimeNG Imports
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

// ABP & Services
import { TenantService } from '../../proxy/volo/abp/tenant-management/tenant.service';
import { TenantDto, GetTenantsInput, TenantCreateDto } from '../../proxy/volo/abp/tenant-management/models';
import { LocalizationModule, LocalizationService, PagedResultDto, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-tenant-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    IconFieldModule,
    InputIconModule,
    ToastModule,
    TagModule,
    SkeletonModule,
    TooltipModule,
    DialogModule,
    MessageModule,
    ConfirmDialogModule,
    LocalizationModule
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './tenant-list.component.html',
  styleUrl: './tenant-list.component.scss'
})
export class TenantListComponent implements OnInit {
  private tenantService = inject(TenantService);
  private messageService = inject(MessageService);
  private confirmationService = inject(ConfirmationService);
  private fb = inject(FormBuilder);
  private localization = inject(LocalizationService);
  private restService = inject(RestService);

  // Table data
  tenants: TenantDto[] = [];
  totalRecords: number = 0;
  loading: boolean = false;

  // Pagination & Sorting
  rows: number = 10;
  first: number = 0;
  
  // Filter
  filterText: string = '';
  private filterTimeout: any;

  // Dialog & Form
  displayCreateDialog: boolean = false;
  createTenantForm!: FormGroup;
  submitting: boolean = false;

  ngOnInit(): void {
    // Initial load is handled by lazy loading event
    this.initializeForm();
  }

  /**
   * Initializes the create tenant form with validators
   */
  private initializeForm(): void {
    this.createTenantForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      adminEmailAddress: ['', [Validators.required, Validators.email]]
    });
  }

  /**
   * Loads tenants with pagination, sorting, and filtering
   */
  loadTenants(event?: TableLazyLoadEvent): void {
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
    const input: GetTenantsInput = {
      skipCount,
      maxResultCount,
      sorting: sorting || undefined,
      filter: this.filterText || undefined
    };

    // Fetch data from API
    this.tenantService.getList(input).subscribe({
      next: (result: PagedResultDto<TenantDto>) => {
        this.tenants = result.items || [];
        this.totalRecords = result.totalCount || 0;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading tenants:', error);
        this.messageService.add({
          severity: 'error',
          summary: this.l('::Common.Error'),
          detail: this.l('::Tenants.Toast.LoadError'),
          life: 3000
        });
        this.loading = false;
        this.tenants = [];
        this.totalRecords = 0;
      }
    });
  }

  /**
   * Handles filter input with debounce
   */
  onFilter(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    
    // Clear previous timeout
    if (this.filterTimeout) {
      clearTimeout(this.filterTimeout);
    }

    // Set new timeout for debounced search
    this.filterTimeout = setTimeout(() => {
      this.filterText = value;
      this.first = 0; // Reset to first page
      this.loadTenants({ first: 0, rows: this.rows });
    }, 500); // 500ms debounce
  }

  /**
   * Clears the filter
   */
  clearFilter(): void {
    this.filterText = '';
    this.first = 0;
    this.loadTenants({ first: 0, rows: this.rows });
  }

  /**
   * Refreshes the table
   */
  refresh(): void {
    this.loadTenants({ first: this.first, rows: this.rows });
  }

  /**
   * Gets the number of Foodics accounts for display
   */
  getFoodicsAccountCount(tenant: TenantDto): number {
    return tenant.foodicsAccounts?.length || 0;
  }

  /**
   * Opens the create tenant dialog
   */
  openCreateDialog(): void {
    this.displayCreateDialog = true;
    this.createTenantForm.reset();
  }

  /**
   * Closes the create tenant dialog
   */
  closeCreateDialog(): void {
    this.displayCreateDialog = false;
    this.createTenantForm.reset();
    this.submitting = false;
  }

  /**
   * Creates a new tenant
   */
  createTenant(): void {
    // Validate form
    if (this.createTenantForm.invalid) {
      Object.keys(this.createTenantForm.controls).forEach(key => {
        this.createTenantForm.controls[key].markAsTouched();
      });
      return;
    }

    this.submitting = true;

    // Prepare DTO (excluding 'test' property)
    const createDto: TenantCreateDto = {
      name: this.createTenantForm.value.name,
      adminEmailAddress: this.createTenantForm.value.adminEmailAddress,
      adminPassword: 'AUTO-GENERATED'
    };

    // Call API
    this.tenantService.create(createDto).subscribe({
      next: (result: TenantDto) => {
        this.messageService.add({
          severity: 'success',
          summary: this.l('::Common.Success'),
          detail: this.l('::Tenants.Toast.CreateSuccess', result.name || ''),
          life: 3000
        });
        this.submitting = false;
        this.closeCreateDialog();
        this.refresh(); // Reload the table
      },
      error: (error) => {
        console.error('Error creating tenant:', error);
        this.messageService.add({
          severity: 'error',
          summary: this.l('::Common.Error'),
          detail: error?.error?.error?.message || this.l('::Tenants.Toast.CreateError'),
          life: 5000
        });
        this.submitting = false;
      }
    });
  }

  /**
   * Helper method to check if a form field has errors and is touched
   */
  isFieldInvalid(fieldName: string): boolean {
    const field = this.createTenantForm.get(fieldName);
    return !!(field && field.invalid && field.touched);
  }

  /**
   * Helper method to get error message for a field
   */
  getFieldError(fieldName: string): string {
    const field = this.createTenantForm.get(fieldName);
    if (field?.hasError('required')) {
      return this.l('::Validation.Required', this.getFieldLabel(fieldName));
    }
    if (field?.hasError('email')) {
      return this.l('::Validation.Email');
    }
    if (field?.hasError('minlength')) {
      const minLength = field.errors?.['minlength']?.requiredLength;
      return this.l('::Validation.MinLength', String(minLength));
    }
    return '';
  }

  /**
   * Helper method to get field label
   */
  private getFieldLabel(fieldName: string): string {
    const labels: { [key: string]: string } = {
      name: this.l('::Tenants.Dialog.NameLabel'),
      adminEmailAddress: this.l('::Tenants.Dialog.AdminEmailLabel')
    };
    return labels[fieldName] || fieldName;
  }

  /**
   * Confirms and deletes a tenant
   */
  deleteTenant(tenant: TenantDto): void {
    this.confirmationService.confirm({
      message: this.l('::Tenants.Confirm.DeleteMessage', tenant.name || ''),
      header: this.l('::Tenants.Confirm.Title'),
      icon: 'pi pi-exclamation-triangle',
      acceptIcon: 'pi pi-check',
      rejectIcon: 'pi pi-times',
      acceptLabel: this.l('::Common.YesDelete'),
      rejectLabel: this.l('::Common.Cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-secondary p-button-outlined',
      accept: () => {
        this.tenantService.delete(tenant.id!).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: this.l('::Common.Deleted'),
              detail: this.l('::Tenants.Toast.DeleteSuccess', tenant.name || ''),
              life: 3000
            });
            this.refresh(); // Reload the table
          },
          error: (error) => {
            console.error('Error deleting tenant:', error);
            this.messageService.add({
              severity: 'error',
              summary: this.l('::Common.Error'),
              detail: error?.error?.error?.message || this.l('::Tenants.Toast.DeleteError'),
              life: 5000
            });
          }
        });
      }
    });
  }

  resendWelcomeEmail(tenant: TenantDto): void {
    if (!tenant.id) {
      return;
    }

    this.confirmationService.confirm({
      message: `Regenerate tenant admin password and resend welcome email for "${tenant.name}"?`,
      header: 'Resend Welcome Email',
      icon: 'pi pi-envelope',
      acceptLabel: this.l('::Common.Yes'),
      rejectLabel: this.l('::Common.Cancel'),
      accept: () => {
        this.restService
          .request<any, void>(
            {
              method: 'POST',
              url: `/api/tenant-admin/${tenant.id}/resend-welcome-email`,
            },
            { apiName: 'Default' }
          )
          .subscribe({
            next: () => {
              this.messageService.add({
                severity: 'success',
                summary: this.l('::Common.Success'),
                detail: `Welcome email resent for "${tenant.name}".`,
                life: 3000,
              });
            },
            error: error => {
              this.messageService.add({
                severity: 'error',
                summary: this.l('::Common.Error'),
                detail: error?.error?.error?.message || 'Failed to resend welcome email.',
                life: 5000,
              });
            },
          });
      },
    });
  }

  private l(key: string, ...interpolateParams: string[]): string {
    return this.localization.instant(key, ...interpolateParams);
  }
}
