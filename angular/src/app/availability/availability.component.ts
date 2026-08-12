import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AvailabilityService, AvailabilityItemDto, AvailabilityBranchStateDto } from './availability.service';


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

  // Out-of-stock dialog state.
  readonly dialogItem = signal<AvailabilityItemDto | null>(null);
  readonly dialogMode = signal<string>('ForADay');
  readonly selectedBranches = signal<Set<string>>(new Set());
  readonly dialogSaving = signal(false);

  // "Out for a day" auto-restore schedule (configurable).
  readonly dayEndTime = signal('05:00');
  readonly timeZone = signal('Asia/Kuwait');
  readonly nextRestore = signal<string | null>(null);
  readonly savingSettings = signal(false);

  ngOnInit(): void {
    this.load();
    this.loadSettings();
  }

  private loadSettings(): void {
    this.svc
      .getSettings()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: s => {
          this.dayEndTime.set(s.dayEndTime || '05:00');
          this.timeZone.set(s.timeZone || 'Asia/Kuwait');
          this.nextRestore.set(s.nextRestoreAtUtc);
        },
        error: () => {},
      });
  }

  saveSettings(): void {
    this.savingSettings.set(true);
    this.svc
      .updateSettings({ dayEndTime: this.dayEndTime(), timeZone: this.timeZone().trim() })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.savingSettings.set(false)))
      .subscribe({
        next: s => {
          this.nextRestore.set(s.nextRestoreAtUtc);
          this.messageService.add({ severity: 'success', summary: 'Restore time saved' });
        },
        error: err =>
          this.messageService.add({
            severity: 'error',
            summary: 'Could not save',
            detail: err?.error?.error?.message || 'Please check the time (HH:mm) and timezone.',
          }),
      });
  }

  key(item: AvailabilityItemDto): string {
    return item.foodicsProductId;
  }

  statusLabel(item: AvailabilityItemDto): string {
    if (item.outOfStockCount === 0) return 'In stock';
    if (item.branchCount <= 1) return 'Out of stock';
    return `Out · ${item.outOfStockCount}/${item.branchCount} branches`;
  }

  outBranchNames(item: AvailabilityItemDto): string {
    return item.branches.filter(b => !b.isInStock).map(b => b.branchName).join(', ');
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

  // ---------- out-of-stock dialog ----------
  openOutDialog(item: AvailabilityItemDto): void {
    this.dialogMode.set('ForADay');
    // Default: all branches selected ("normally applies to all branches").
    this.selectedBranches.set(new Set(item.branches.map(b => b.vendorCode)));
    this.dialogItem.set(item);
  }

  closeDialog(): void {
    this.dialogItem.set(null);
  }

  isBranchSelected(vendorCode: string): boolean {
    return this.selectedBranches().has(vendorCode);
  }

  toggleBranch(vendorCode: string): void {
    const next = new Set(this.selectedBranches());
    if (next.has(vendorCode)) {
      next.delete(vendorCode);
    } else {
      next.add(vendorCode);
    }
    this.selectedBranches.set(next);
  }

  allBranchesSelected(): boolean {
    const item = this.dialogItem();
    return !!item && item.branches.length > 0 && this.selectedBranches().size === item.branches.length;
  }

  toggleAllBranches(): void {
    const item = this.dialogItem();
    if (!item) return;
    this.selectedBranches.set(
      this.allBranchesSelected() ? new Set() : new Set(item.branches.map(b => b.vendorCode)),
    );
  }

  confirmOut(): void {
    const item = this.dialogItem();
    if (!item) return;
    const vendorCodes = [...this.selectedBranches()];
    if (vendorCodes.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Pick at least one branch' });
      return;
    }
    this.dialogSaving.set(true);
    this.svc
      .setAvailability({ foodicsProductIds: [item.foodicsProductId], vendorCodes, inStock: false, mode: this.dialogMode() })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.dialogSaving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Marked out of stock',
            detail: `${vendorCodes.length} branch(es)`,
          });
          this.closeDialog();
          this.load();
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Update failed' }),
      });
  }

  // ---------- restore ----------
  markInStock(item: AvailabilityItemDto): void {
    const vendorCodes = item.branches.filter(b => !b.isInStock).map(b => b.vendorCode);
    if (vendorCodes.length === 0) return;
    this.saving.set(this.key(item));
    this.svc
      .setAvailability({ foodicsProductIds: [item.foodicsProductId], vendorCodes, inStock: true })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Marked in stock' });
          this.load();
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Update failed' }),
      });
  }
}
