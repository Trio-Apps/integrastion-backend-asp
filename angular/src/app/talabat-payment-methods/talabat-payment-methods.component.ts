import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import {
  TalabatPaymentMethodDto,
  TalabatPaymentMethodSettingsDto,
  TalabatPaymentMethodsService,
} from './talabat-payment-methods.service';

@Component({
  selector: 'app-talabat-payment-methods',
  standalone: true,
  imports: [CommonModule, ButtonModule, TableModule, TagModule, ToastModule],
  templateUrl: './talabat-payment-methods.component.html',
  styleUrls: ['./talabat-payment-methods.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class TalabatPaymentMethodsComponent implements OnInit {
  private readonly service = inject(TalabatPaymentMethodsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  readonly loading = signal<boolean>(false);
  readonly savingPaymentMethodId = signal<string | null>(null);
  readonly settings = signal<TalabatPaymentMethodSettingsDto | null>(null);
  readonly paymentMethods = computed(() => this.settings()?.paymentMethods ?? []);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);

    this.service
      .getSettings()
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: settings => this.settings.set(settings),
        error: error => {
          console.error('Failed to load Talabat payment methods', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Load failed',
            detail: error?.error?.error?.message || 'Unable to load payment methods from Foodics.',
          });
        },
      });
  }

  setActive(paymentMethodId: string | null): void {
    this.savingPaymentMethodId.set(paymentMethodId ?? '__clear__');

    this.service
      .updateActive({ paymentMethodId })
      .pipe(finalize(() => this.savingPaymentMethodId.set(null)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: settings => {
          this.settings.set(settings);
          this.messageService.add({
            severity: 'success',
            summary: 'Saved',
            detail: paymentMethodId
              ? 'The active payment method has been updated.'
              : 'The active payment method has been cleared.',
          });
        },
        error: error => {
          console.error('Failed to update active payment method', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Save failed',
            detail: error?.error?.error?.message || 'Unable to save the selected payment method.',
          });
        },
      });
  }

  isActive(method: TalabatPaymentMethodDto): boolean {
    return this.settings()?.activePaymentMethodId === method.id;
  }

  getTypeLabel(type?: number): string {
    switch (type) {
      case 1:
        return 'Cash';
      case 2:
        return 'Card';
      case 3:
        return 'Credit';
      default:
        return type == null ? '-' : `Type ${type}`;
    }
  }

  getStatusSeverity(method: TalabatPaymentMethodDto): 'success' | 'secondary' {
    return method.isActive ? 'success' : 'secondary';
  }

  isSaving(method: TalabatPaymentMethodDto): boolean {
    return this.savingPaymentMethodId() === method.id;
  }

  isClearing(): boolean {
    return this.savingPaymentMethodId() === '__clear__';
  }
}
