import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressBarModule } from 'primeng/progressbar';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import {
  MenuSyncDiagnosticsService,
  MenuSyncRunDetailsDto,
  MenuSyncRunSummaryDto,
  MenuSyncVendorItemDto,
  MenuSyncVendorSubmissionDto,
} from './menu-sync-diagnostics.service';

@Component({
  selector: 'app-menu-sync-diagnostics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    ProgressBarModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './menu-sync-diagnostics.component.html',
  styleUrls: ['./menu-sync-diagnostics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MenuSyncDiagnosticsComponent implements OnInit {
  private readonly service = inject(MenuSyncDiagnosticsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loadingRuns = signal(false);
  readonly loadingDetails = signal(false);
  readonly loadingItems = signal(false);
  readonly runs = signal<MenuSyncRunSummaryDto[]>([]);
  readonly totalRecords = signal(0);
  readonly rows = signal(10);
  readonly first = signal(0);

  readonly selectedRun = signal<MenuSyncRunDetailsDto | null>(null);
  readonly selectedVendor = signal<MenuSyncVendorSubmissionDto | null>(null);
  readonly vendorItems = signal<MenuSyncVendorItemDto[]>([]);
  readonly itemSearch = signal('');
  readonly jsonDialogVisible = signal(false);
  readonly jsonDialogTitle = signal('');
  readonly jsonDialogContent = signal('');

  readonly searchTerm = signal('');
  readonly foodicsAccountId = signal('');
  readonly status = signal('');
  readonly fromDate = signal('');
  readonly toDate = signal('');
  readonly isDetailsRoute = signal(false);

  readonly statusOptions = [
    { label: 'All', value: '' },
    { label: 'Completed', value: 'Completed' },
    { label: 'Running', value: 'Running' },
    { label: 'Failed', value: 'Failed' },
    { label: 'Cancelled', value: 'Cancelled' },
  ];

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const runId = params.get('id');
      this.isDetailsRoute.set(!!runId);
      this.selectedVendor.set(null);
      this.vendorItems.set([]);

      if (runId) {
        this.loadRunDetails(runId);
        return;
      }

      this.selectedRun.set(null);
    });
  }

  readonly filteredItems = computed(() => {
    const term = this.itemSearch().trim().toLowerCase();
    if (!term) {
      return this.vendorItems();
    }

    return this.vendorItems().filter(item =>
      [
        item.foodicsProductId,
        item.name,
        item.nameLocalized,
        item.categoryName,
        item.talabatSyncStatus,
        item.talabatImportId,
      ]
        .filter(Boolean)
        .some(value => String(value).toLowerCase().includes(term))
    );
  });

  refresh(): void {
    this.loadRuns({ first: 0, rows: this.rows() });
  }

  loadRuns(event?: TableLazyLoadEvent): void {
    this.loadingRuns.set(true);

    const skipCount = event?.first ?? 0;
    const maxResultCount = event?.rows ?? this.rows();
    let sorting = '';
    if (event?.sortField) {
      sorting = `${event.sortField} ${event.sortOrder === 1 ? 'asc' : 'desc'}`;
    }

    this.service
      .getRuns({
        skipCount,
        maxResultCount,
        sorting: sorting || undefined,
        searchTerm: this.searchTerm().trim() || undefined,
        foodicsAccountId: this.foodicsAccountId().trim() || undefined,
        status: this.status() || undefined,
        fromDate: this.toUtcIso(this.fromDate()),
        toDate: this.toUtcIso(this.toDate()),
      })
      .pipe(finalize(() => this.loadingRuns.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.runs.set(result.items || []);
          this.totalRecords.set(result.totalCount || 0);
          this.first.set(skipCount);
        },
        error: error => {
          console.error('Failed to load sync runs', error);
          this.runs.set([]);
          this.totalRecords.set(0);
        },
      });
  }

  openRun(run: MenuSyncRunSummaryDto): void {
    this.router.navigate(['/menu-sync-diagnostics', run.id]);
  }

  backToRuns(): void {
    this.router.navigate(['/menu-sync-diagnostics']);
  }

  refreshCurrentRun(): void {
    const run = this.selectedRun();
    if (run) {
      this.loadRunDetails(run.id);
    }
  }

  private loadRunDetails(runId: string): void {
    this.loadingDetails.set(true);
    this.selectedVendor.set(null);
    this.vendorItems.set([]);

    this.service
      .getRunDetails(runId)
      .pipe(finalize(() => this.loadingDetails.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: details => this.selectedRun.set(details),
        error: error => {
          console.error('Failed to load sync run details', error);
          this.selectedRun.set(null);
        },
      });
  }

  openVendor(vendor: MenuSyncVendorSubmissionDto): void {
    const run = this.selectedRun();
    if (!run) {
      return;
    }

    this.selectedVendor.set(vendor);
    this.loadingItems.set(true);
    this.itemSearch.set('');

    this.service
      .getVendorItems(run.id, vendor.vendorCode)
      .pipe(finalize(() => this.loadingItems.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: items => this.vendorItems.set(items || []),
        error: error => {
          console.error('Failed to load vendor items', error);
          this.vendorItems.set([]);
        },
      });
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.foodicsAccountId.set('');
    this.status.set('');
    this.fromDate.set('');
    this.toDate.set('');
    this.refresh();
  }

  showJson(title: string, value?: string): void {
    this.jsonDialogTitle.set(title);
    this.jsonDialogContent.set(this.prettyJson(value));
    this.jsonDialogVisible.set(true);
  }

  getStatusSeverity(status?: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
    switch ((status || '').toLowerCase()) {
      case 'completed':
      case 'done':
      case 'success':
      case 'succeeded':
        return 'success';
      case 'failed':
      case 'error':
      case 'partial':
        return 'danger';
      case 'running':
      case 'submitted':
      case 'processing':
      case 'in_progress':
        return 'info';
      case 'pending':
      case 'notrecorded':
        return 'warn';
      default:
        return 'secondary';
    }
  }

  formatDate(value?: string): string {
    if (!value) {
      return '-';
    }

    const parsed = new Date(value.endsWith('Z') ? value : `${value}Z`);
    if (Number.isNaN(parsed.getTime())) {
      return '-';
    }

    return this.dateFormatter.format(parsed);
  }

  formatDuration(seconds?: number): string {
    if (seconds === undefined || seconds === null) {
      return '-';
    }

    if (seconds < 60) {
      return `${Math.round(seconds)}s`;
    }

    const minutes = Math.floor(seconds / 60);
    const remaining = Math.round(seconds % 60);
    return `${minutes}m ${remaining}s`;
  }

  private prettyJson(value?: string): string {
    if (!value?.trim()) {
      return '-';
    }

    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }

  private toUtcIso(value: string): string | undefined {
    if (!value) {
      return undefined;
    }

    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
  }

  private readonly dateFormatter = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Riyadh',
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    second: '2-digit',
    hour12: true,
  });
}
