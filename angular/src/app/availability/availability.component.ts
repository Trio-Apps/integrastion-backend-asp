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

  // Row selection for bulk actions (keyed by foodicsProductId).
  readonly selectedItems = signal<Set<string>>(new Set());
  readonly bulkSaving = signal(false);

  // Out-of-stock dialog state. Holds one item for a row action, or many for a bulk action.
  readonly dialogItems = signal<AvailabilityItemDto[]>([]);
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
    this.clearSelection();
    this.load();
  }

  page(dir: number): void {
    const next = this.first() + dir * this.rows;
    if (next < 0 || next >= this.total()) return;
    this.first.set(next);
    this.clearSelection();
    this.load();
  }

  // ---------- bulk selection ----------
  // Selection is cleared when the page or search changes, so a bulk action can never
  // touch rows the user can't see.
  isItemSelected(item: AvailabilityItemDto): boolean {
    return this.selectedItems().has(item.foodicsProductId);
  }

  toggleItem(item: AvailabilityItemDto): void {
    const next = new Set(this.selectedItems());
    if (next.has(item.foodicsProductId)) {
      next.delete(item.foodicsProductId);
    } else {
      next.add(item.foodicsProductId);
    }
    this.selectedItems.set(next);
  }

  allOnPageSelected(): boolean {
    const rows = this.items();
    return rows.length > 0 && rows.every(i => this.selectedItems().has(i.foodicsProductId));
  }

  toggleAllOnPage(): void {
    this.selectedItems.set(
      this.allOnPageSelected() ? new Set() : new Set(this.items().map(i => i.foodicsProductId)),
    );
  }

  clearSelection(): void {
    this.selectedItems.set(new Set());
  }

  selectedRows(): AvailabilityItemDto[] {
    return this.items().filter(i => this.selectedItems().has(i.foodicsProductId));
  }

  /** Selected rows that currently have at least one branch out of stock. */
  selectedRestorable(): AvailabilityItemDto[] {
    return this.selectedRows().filter(i => i.outOfStockCount > 0);
  }

  // ---------- out-of-stock dialog ----------
  openOutDialog(item: AvailabilityItemDto): void {
    this.openDialogFor([item]);
  }

  openBulkOutDialog(): void {
    const rows = this.selectedRows();
    if (rows.length) {
      this.openDialogFor(rows);
    }
  }

  private openDialogFor(items: AvailabilityItemDto[]): void {
    this.dialogMode.set('ForADay');
    this.dialogItems.set(items);
    // Default: all branches selected ("normally applies to all branches").
    this.selectedBranches.set(new Set(this.dialogBranches().map(b => b.vendorCode)));
  }

  /** Branches to offer in the dialog: the union across the items being changed. */
  dialogBranches(): AvailabilityBranchStateDto[] {
    const byCode = new Map<string, AvailabilityBranchStateDto>();
    for (const item of this.dialogItems()) {
      for (const b of item.branches) {
        if (!byCode.has(b.vendorCode)) {
          byCode.set(b.vendorCode, b);
        }
      }
    }
    return [...byCode.values()];
  }

  dialogTitle(): string {
    const items = this.dialogItems();
    return items.length === 1 ? items[0].name : `${items.length} items selected`;
  }

  closeDialog(): void {
    this.dialogItems.set([]);
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
    const branches = this.dialogBranches();
    return branches.length > 0 && this.selectedBranches().size === branches.length;
  }

  toggleAllBranches(): void {
    this.selectedBranches.set(
      this.allBranchesSelected() ? new Set() : new Set(this.dialogBranches().map(b => b.vendorCode)),
    );
  }

  confirmOut(): void {
    const items = this.dialogItems();
    if (!items.length) return;
    const vendorCodes = [...this.selectedBranches()];
    if (vendorCodes.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'Pick at least one branch' });
      return;
    }
    const mode = this.dialogMode();
    this.dialogSaving.set(true);
    this.svc
      .setAvailability({
        foodicsProductIds: items.map(i => i.foodicsProductId),
        vendorCodes,
        inStock: false,
        mode,
      })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.dialogSaving.set(false)))
      .subscribe({
        next: () => {
          // Update the rows in place (no full reload) so it feels instant.
          const sel = new Set(vendorCodes);
          for (const item of items) {
            this.patchItem(item, b => (sel.has(b.vendorCode) ? { ...b, isInStock: false, mode } : b));
          }
          this.messageService.add({
            severity: 'success',
            summary: 'Marked out of stock',
            detail:
              items.length === 1
                ? `${vendorCodes.length} branch(es)`
                : `${items.length} items · ${vendorCodes.length} branch(es)`,
          });
          this.clearSelection();
          this.closeDialog();
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Update failed' }),
      });
  }

  private patchItem(item: AvailabilityItemDto, map: (b: AvailabilityBranchStateDto) => AvailabilityBranchStateDto): void {
    item.branches = item.branches.map(map);
    item.outOfStockCount = item.branches.filter(b => !b.isInStock).length;
    this.items.set([...this.items()]);
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
          this.patchItem(item, b => (b.isInStock ? b : { ...b, isInStock: true, mode: undefined, restoreAtUtc: undefined }));
          this.messageService.add({ severity: 'success', summary: 'Marked in stock' });
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Update failed' }),
      });
  }

  /** Restore every out-of-stock branch across the selected rows. */
  bulkMarkInStock(): void {
    const items = this.selectedRestorable();
    if (!items.length) return;

    const codes = new Set<string>();
    for (const item of items) {
      for (const b of item.branches) {
        if (!b.isInStock) {
          codes.add(b.vendorCode);
        }
      }
    }
    const vendorCodes = [...codes];

    this.bulkSaving.set(true);
    this.svc
      .setAvailability({ foodicsProductIds: items.map(i => i.foodicsProductId), vendorCodes, inStock: true })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.bulkSaving.set(false)))
      .subscribe({
        next: () => {
          for (const item of items) {
            this.patchItem(item, b =>
              b.isInStock ? b : { ...b, isInStock: true, mode: undefined, restoreAtUtc: undefined },
            );
          }
          this.messageService.add({
            severity: 'success',
            summary: 'Marked in stock',
            detail: `${items.length} item(s)`,
          });
          this.clearSelection();
        },
        error: () => this.messageService.add({ severity: 'error', summary: 'Update failed' }),
      });
  }
}
