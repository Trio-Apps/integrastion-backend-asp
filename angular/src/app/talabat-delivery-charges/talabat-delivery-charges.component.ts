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
  TalabatDeliveryChargeDto,
  TalabatDeliveryChargeSettingsDto,
  TalabatDeliveryChargesService,
} from './talabat-delivery-charges.service';

@Component({
  selector: 'app-talabat-delivery-charges',
  standalone: true,
  imports: [CommonModule, ButtonModule, TableModule, TagModule, ToastModule],
  templateUrl: './talabat-delivery-charges.component.html',
  styleUrls: ['./talabat-delivery-charges.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class TalabatDeliveryChargesComponent implements OnInit {
  private readonly service = inject(TalabatDeliveryChargesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  readonly loading = signal<boolean>(false);
  readonly savingChargeId = signal<string | null>(null);
  readonly settings = signal<TalabatDeliveryChargeSettingsDto | null>(null);
  readonly charges = computed(() => this.settings()?.charges ?? []);

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
          console.error('Failed to load Talabat delivery charges', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Load failed',
            detail: error?.error?.error?.message || 'Unable to load delivery charges from Foodics.',
          });
        },
      });
  }

  setActive(deliveryChargeId: string | null): void {
    this.savingChargeId.set(deliveryChargeId ?? '__clear__');

    this.service
      .updateActive({ deliveryChargeId })
      .pipe(finalize(() => this.savingChargeId.set(null)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: settings => {
          this.settings.set(settings);
          this.messageService.add({
            severity: 'success',
            summary: 'Saved',
            detail: deliveryChargeId
              ? 'The active delivery charge has been updated.'
              : 'The active delivery charge has been cleared.',
          });
        },
        error: error => {
          console.error('Failed to update active delivery charge', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Save failed',
            detail: error?.error?.error?.message || 'Unable to save the selected delivery charge.',
          });
        },
      });
  }

  isActive(charge: TalabatDeliveryChargeDto): boolean {
    return this.settings()?.activeDeliveryChargeId === charge.id;
  }

  isSaving(charge: TalabatDeliveryChargeDto): boolean {
    return this.savingChargeId() === charge.id;
  }

  isClearing(): boolean {
    return this.savingChargeId() === '__clear__';
  }

  getTypeLabel(type: number): string {
    switch (type) {
      case 1:
        return 'Amount';
      case 2:
        return 'Percent';
      default:
        return `Type ${type}`;
    }
  }

  getOrderTypesLabel(orderTypes: number[]): string {
    if (!orderTypes?.length) {
      return '-';
    }

    return orderTypes
      .map(type => {
        switch (type) {
          case 1:
            return 'Delivery';
          case 2:
            return 'Pickup';
          case 3:
            return 'Dine-in';
          case 4:
            return 'Drive-thru';
          default:
            return `Type ${type}`;
        }
      })
      .join(', ');
  }
}
