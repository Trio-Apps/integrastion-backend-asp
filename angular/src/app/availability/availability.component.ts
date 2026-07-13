import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AvailabilityService, AvailabilityItemDto } from './availability.service';

@Component({
  selector: 'app-availability',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './availability.component.html',
  styleUrls: ['./availability.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AvailabilityComponent implements OnInit {
  private readonly svc = inject(AvailabilityService);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  readonly items = signal<AvailabilityItemDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly saving = signal<string | null>(null); // key of the row being saved

  readonly search = signal('');
  readonly rows = 15;
  readonly first = signal(0);

  ngOnInit(): void {
    this.load();
  }

  key(item: AvailabilityItemDto): string {
    return item.foodicsProductId + '|' + item.vendorCode;
  }

  load(): void {
    this.loading.set(true);
    this.svc
      .getItems({ search: this.search().trim() || undefined, maxResultCount: this.rows, skipCount: this.first() })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => {
          this.items.set(r.items ?? []);
          this.total.set(r.totalCount ?? 0);
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Failed to load items' }),
      });
  }

  onSearch(): void {
    this.first.set(0);
    this.load();
  }

  page(dir: number): void {
    const next = this.first() + dir * this.rows;
    if (next < 0 || next >= this.total()) return;
    this.first.set(next);
    this.load();
  }

  set(item: AvailabilityItemDto, inStock: boolean, mode?: string): void {
    this.saving.set(this.key(item));
    this.svc
      .setAvailability({ foodicsProductIds: [item.foodicsProductId], vendorCodes: [item.vendorCode], inStock, mode })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(null)))
      .subscribe({
        next: () => {
          item.isInStock = inStock;
          item.mode = inStock ? undefined : mode;
          this.items.set([...this.items()]);
          this.messageService.add({ severity: 'success', summary: inStock ? 'Marked in stock' : 'Marked out of stock' });
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Update failed' }),
      });
  }
}
