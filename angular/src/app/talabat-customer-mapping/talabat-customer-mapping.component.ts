import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { Select } from 'primeng/select';
import {
  FoodicsAddressLookupDto,
  FoodicsCustomerLookupDto,
  TalabatCustomerMappingAccountDto,
  TalabatCustomerMappingService,
} from './talabat-customer-mapping.service';

@Component({
  selector: 'app-talabat-customer-mapping',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, ToastModule, Select],
  templateUrl: './talabat-customer-mapping.component.html',
  styleUrls: ['./talabat-customer-mapping.component.scss'],
  providers: [MessageService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TalabatCustomerMappingComponent implements OnInit {
  private readonly service = inject(TalabatCustomerMappingService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  readonly loading = signal(false);
  readonly customerLoading = signal(false);
  readonly addressLoading = signal(false);
  readonly saving = signal(false);

  readonly accounts = signal<TalabatCustomerMappingAccountDto[]>([]);
  readonly selectedTalabatAccountId = signal<string | null>(null);
  readonly customers = signal<FoodicsCustomerLookupDto[]>([]);
  readonly addresses = signal<FoodicsAddressLookupDto[]>([]);
  readonly selectedCustomerId = signal<string | null>(null);
  readonly selectedAddressId = signal<string | null>(null);

  readonly selectedAccount = computed(() =>
    this.accounts().find(x => x.talabatAccountId === this.selectedTalabatAccountId()) ?? null,
  );

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.service.getSettings()
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: settings => {
          this.accounts.set(settings.accounts ?? []);
          const fallbackAccountId = settings.accounts?.[0]?.talabatAccountId ?? null;
          const currentAccountId = this.selectedTalabatAccountId();
          const nextAccountId = settings.accounts.some(x => x.talabatAccountId === currentAccountId)
            ? currentAccountId
            : fallbackAccountId;

          this.selectedTalabatAccountId.set(nextAccountId);
          this.syncSelectionFromAccount();
        },
        error: error => {
          console.error('Failed to load customer mapping settings', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Load failed',
            detail: error?.error?.error?.message || 'Unable to load Talabat customer mappings.',
          });
        },
      });
  }

  onTalabatAccountChange(accountId: string | null): void {
    this.selectedTalabatAccountId.set(accountId);
    this.syncSelectionFromAccount();
  }

  onCustomerChange(customerId: string | null): void {
    this.selectedCustomerId.set(customerId);
    this.selectedAddressId.set(null);
    this.addresses.set([]);

    const customer = this.customers().find(x => x.id === customerId);
    if (!customerId || !customer) {
      return;
    }

    this.loadAddresses(customerId);
  }

  onCustomerFilter(event: { filter?: string }): void {
    this.loadCustomers(event?.filter ?? '');
  }

  save(): void {
    const account = this.selectedAccount();
    if (!account) {
      return;
    }

    const customer = this.customers().find(x => x.id === this.selectedCustomerId());
    if (!customer) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Customer required',
        detail: 'Select a Foodics customer before saving.',
      });
      return;
    }

    const address = this.addresses().find(x => x.id === this.selectedAddressId());
    if (!address) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Address required',
        detail: 'Select a Foodics address before saving.',
      });
      return;
    }

    this.saving.set(true);
    this.service.update({
      talabatAccountId: account.talabatAccountId,
      customerId: customer.id,
      customerName: customer.name ?? customer.phone ?? customer.id,
      customerAddressId: address.id,
      customerAddressName: address.name ?? address.description ?? address.id,
    })
    .pipe(finalize(() => this.saving.set(false)), takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: settings => {
        this.accounts.set(settings.accounts ?? []);
        this.messageService.add({
          severity: 'success',
          summary: 'Saved',
          detail: 'Default Foodics customer mapping has been updated.',
        });
        this.syncSelectionFromAccount();
      },
      error: error => {
        console.error('Failed to save customer mapping', error);
        this.messageService.add({
          severity: 'error',
          summary: 'Save failed',
          detail: error?.error?.error?.message || 'Unable to save the selected customer mapping.',
        });
      },
    });
  }

  clearMapping(): void {
    const account = this.selectedAccount();
    if (!account) {
      return;
    }

    this.saving.set(true);
    this.service.update({
      talabatAccountId: account.talabatAccountId,
      customerId: null,
      customerName: null,
      customerAddressId: null,
      customerAddressName: null,
    })
    .pipe(finalize(() => this.saving.set(false)), takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: settings => {
        this.accounts.set(settings.accounts ?? []);
        this.messageService.add({
          severity: 'success',
          summary: 'Cleared',
          detail: 'Default Foodics customer mapping has been cleared.',
        });
        this.syncSelectionFromAccount();
      },
      error: error => {
        console.error('Failed to clear customer mapping', error);
        this.messageService.add({
          severity: 'error',
          summary: 'Clear failed',
          detail: error?.error?.error?.message || 'Unable to clear the customer mapping.',
        });
      },
    });
  }

  customerLabel(customer: FoodicsCustomerLookupDto): string {
    const dialCode = customer.dialCode ? `+${customer.dialCode}` : '';
    const phone = customer.phone ? `${dialCode}${customer.phone}` : '';
    return [customer.name, phone, customer.email].filter(Boolean).join(' - ');
  }

  addressLabel(address: FoodicsAddressLookupDto): string {
    return [address.name, address.description].filter(Boolean).join(' - ') || address.id;
  }

  private syncSelectionFromAccount(): void {
    const account = this.selectedAccount();
    this.customers.set([]);
    this.addresses.set([]);
    this.selectedCustomerId.set(account?.defaultCustomerId ?? null);
    this.selectedAddressId.set(account?.defaultCustomerAddressId ?? null);

    if (account) {
      this.loadCustomers('');
    }
  }

  private loadCustomers(filter: string): void {
    const account = this.selectedAccount();
    if (!account) {
      this.customers.set([]);
      return;
    }

    this.customerLoading.set(true);
    this.service.searchCustomers(account.talabatAccountId, filter || undefined)
      .pipe(finalize(() => this.customerLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: customers => {
          this.customers.set(customers ?? []);

          const selectedCustomerId = this.selectedCustomerId();
          const selectedCustomerExists = !!selectedCustomerId && customers.some(x => x.id === selectedCustomerId);
          if (selectedCustomerExists) {
            this.loadAddresses(selectedCustomerId!);
          } else {
            this.addresses.set([]);
            this.selectedAddressId.set(null);
          }
        },
        error: error => {
          console.error('Failed to load Foodics customers', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Customers load failed',
            detail: error?.error?.error?.message || 'Unable to load Foodics customers.',
          });
        },
      });
  }

  private loadAddresses(customerId: string): void {
    const account = this.selectedAccount();
    if (!account || !customerId) {
      this.addresses.set([]);
      return;
    }

    this.addressLoading.set(true);
    this.service.getAddresses(account.talabatAccountId, customerId)
      .pipe(finalize(() => this.addressLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: addresses => {
          this.addresses.set(addresses ?? []);
          const selectedAddressId = this.selectedAddressId();
          if (selectedAddressId && !addresses.some(x => x.id === selectedAddressId)) {
            this.selectedAddressId.set(null);
          }
        },
        error: error => {
          console.error('Failed to load Foodics addresses', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Addresses load failed',
            detail: error?.error?.error?.message || 'Unable to load Foodics customer addresses.',
          });
        },
      });
  }
}
