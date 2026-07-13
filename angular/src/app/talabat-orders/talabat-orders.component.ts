import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { interval } from 'rxjs';
import { filter, finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LocalizationModule } from '@abp/ng.core';
import { TableModule, TableLazyLoadEvent, TableRowExpandEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { TalabatOrderLogsService, TalabatOrderLogDto, TalabatOrderDetailsDto, GetTalabatOrderLogsInput } from '@proxy/talabat';

@Component({
  selector: 'app-talabat-orders',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    LocalizationModule,
    TableModule,
    TagModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    ToastModule,
  ],
  templateUrl: './talabat-orders.component.html',
  styleUrls: ['./talabat-orders.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class TalabatOrdersComponent {
  private readonly orderLogsService = inject(TalabatOrderLogsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  private readonly POLL_INTERVAL_MS = 30_000;

  readonly loading = signal<boolean>(false);
  readonly bulkRetrying = signal<boolean>(false);
  readonly logs = signal<TalabatOrderLogDto[]>([]);
  readonly totalRecords = signal<number>(0);
  readonly rows = signal<number>(10);
  readonly first = signal<number>(0);
  readonly autoRefreshEnabled = signal<boolean>(true);
  readonly lastRefreshedAt = signal<Date | null>(null);

  constructor() {
    interval(this.POLL_INTERVAL_MS)
      .pipe(
        filter(() => this.autoRefreshEnabled() && !this.loading() && this.first() === 0),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.loadLogs({ first: 0, rows: this.rows() }));

    this.orderLogsService.getAccessibleBranches()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: list => this.branches.set(list.map(b => ({ value: b.branchId, label: b.branchName || b.branchId }))),
        error: () => {},
      });
  }

  readonly searchTerm = signal<string>('');
  readonly vendorCode = signal<string>('');
  readonly status = signal<string>('');
  readonly branchId = signal<string>('');
  readonly customerName = signal<string>('');
  readonly customerPhone = signal<string>('');
  readonly fromDate = signal<string>('');
  readonly toDate = signal<string>('');
  readonly branches = signal<{ value: string; label: string }[]>([]);
  readonly errorDialogVisible = signal<boolean>(false);
  readonly selectedErrorMessage = signal<string>('');

  expandedRows: { [key: string]: boolean } = {};
  private readonly detailsCache = new Map<string, TalabatOrderDetailsDto>();
  private readonly detailsLoadingSet = new Set<string>();

  readonly statusOptions = [
    { label: 'All', value: '' },
    { label: 'Enqueued', value: 'Enqueued' },
    { label: 'Processing', value: 'Processing' },
    { label: 'Completed', value: 'Completed' },
    { label: 'Failed', value: 'Failed' },
  ];

  refresh(): void {
    this.loadLogs({ first: 0, rows: this.rows() });
  }

  loadLogs(event?: TableLazyLoadEvent): void {
    this.loading.set(true);

    const skipCount = event?.first ?? 0;
    const maxResultCount = event?.rows ?? this.rows();

    let sorting = '';
    if (event?.sortField) {
      sorting = `${event.sortField} ${event.sortOrder === 1 ? 'asc' : 'desc'}`;
    }

    const input: GetTalabatOrderLogsInput = {
      skipCount,
      maxResultCount,
      sorting: sorting || undefined,
      searchTerm: this.searchTerm().trim() || undefined,
      vendorCode: this.vendorCode().trim() || undefined,
      branchId: this.branchId() || undefined,
      customerName: this.customerName().trim() || undefined,
      customerPhone: this.customerPhone().trim() || undefined,
      status: this.status() || undefined,
      fromDate: this.fromDate() || undefined,
      toDate: this.toDate() || undefined,
    };

    this.orderLogsService
      .getList(input)
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.logs.set(result.items || []);
          this.totalRecords.set(result.totalCount || 0);
          this.first.set(skipCount);
          this.lastRefreshedAt.set(new Date());
        },
        error: error => {
          console.error('Failed to load Talabat order logs', error);
          this.logs.set([]);
          this.totalRecords.set(0);
        },
      });
  }

  getStatusSeverity(status?: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
    switch ((status || '').toLowerCase()) {
      case 'completed':
      case 'done':
        return 'success';
      case 'failed':
      case 'error':
        return 'danger';
      case 'processing':
        return 'info';
      case 'enqueued':
      case 'pending':
        return 'warn';
      default:
        return 'secondary';
    }
  }

  getOrderCode(log: TalabatOrderLogDto): string {
    return log.shortCode || log.orderCode || log.orderToken || '-';
  }

  formatReceivedAt(log: TalabatOrderLogDto): string {
    const parsed = this.parsePossiblyUtcDate(log.receivedAt || log.creationTime);
    return parsed ? this.saudiDateTimeFormatter.format(parsed) : '-';
  }

  private parsePossiblyUtcDate(value?: string | Date): Date | null {
    if (!value) {
      return null;
    }

    if (value instanceof Date) {
      return Number.isNaN(value.getTime()) ? null : value;
    }

    const raw = value.trim();
    if (!raw) {
      return null;
    }

    const hasExplicitTimezone = /(?:[zZ]|[+\-]\d{2}:\d{2})$/.test(raw);
    const normalized = hasExplicitTimezone ? raw : `${raw}Z`;
    const parsed = new Date(normalized);

    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  private readonly saudiDateTimeFormatter = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Riyadh',
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });

  openErrorDialog(log: TalabatOrderLogDto): void {
    const message = log.lastError?.trim();
    if (!message) {
      return;
    }

    this.selectedErrorMessage.set(message);
    this.errorDialogVisible.set(true);
  }

  isRetryable(log: TalabatOrderLogDto): boolean {
    const status = (log.status || '').toLowerCase();
    return status === 'failed' || status === 'enqueued';
  }

  retryOrder(log: TalabatOrderLogDto): void {
    if (!log.id) {
      return;
    }

    this.orderLogsService.retry(log.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Retry queued',
            detail: 'Order retry has been queued successfully.'
          });
          this.refresh();
        },
        error: error => {
          console.error('Failed to retry order', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Retry failed',
            detail: error?.error?.error?.message || 'Unable to retry this order.'
          });
        }
      });
  }

  retryFailedAndEnqueued(): void {
    const vendor = this.vendorCode().trim();
    const scope = vendor ? ` for ${vendor}` : '';
    const confirmed = window.confirm(`Retry all failed and stuck enqueued orders${scope}?`);
    if (!confirmed) {
      return;
    }

    this.bulkRetrying.set(true);
    this.orderLogsService.retryFailedAndEnqueued({
      vendorCode: vendor || undefined,
      includeEnqueued: true,
    })
      .pipe(finalize(() => this.bulkRetrying.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.messageService.add({
            severity: 'success',
            summary: 'Bulk retry queued',
            detail: `${result.queuedCount || 0} order(s) queued. ${result.skippedCount || 0} skipped.`
          });
          this.refresh();
        },
        error: error => {
          console.error('Failed to bulk retry orders', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Bulk retry failed',
            detail: error?.error?.error?.message || 'Unable to retry failed orders.'
          });
        }
      });
  }

  onRowExpand(event: TableRowExpandEvent): void {
    const id: string | undefined = (event.data as TalabatOrderLogDto).id;
    if (!id || this.detailsCache.has(id) || this.detailsLoadingSet.has(id)) return;

    this.detailsLoadingSet.add(id);
    this.orderLogsService.getDetails(id)
      .pipe(finalize(() => this.detailsLoadingSet.delete(id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: details => this.detailsCache.set(id, details),
        error: err => console.error('Failed to load order details', err),
      });
  }

  getOrderDetails(id?: string): TalabatOrderDetailsDto | null {
    if (!id) return null;
    return this.detailsCache.get(id) ?? null;
  }

  isDetailsLoading(id?: string): boolean {
    return !!id && this.detailsLoadingSet.has(id);
  }

  formatCurrency(value?: number | null): string {
    if (value == null) return '-';
    return value.toFixed(2);
  }

  formatExpeditionType(type?: string | null): string {
    if (!type) return '-';
    const upper = type.toUpperCase();
    if (upper === 'TMP' || upper.includes('TALABAT')) return 'TMP';
    if (upper === 'TGO' || upper.includes('OWN') || upper.includes('VENDOR')) return 'TGO';
    return type;
  }

  toggleAutoRefresh(): void {
    this.autoRefreshEnabled.set(!this.autoRefreshEnabled());
  }

  formatLastRefreshed(): string {
    const d = this.lastRefreshedAt();
    if (!d) return '';
    return d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', second: '2-digit', hour12: true });
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.vendorCode.set('');
    this.status.set('');
    this.branchId.set('');
    this.customerName.set('');
    this.customerPhone.set('');
    this.fromDate.set('');
    this.toDate.set('');
    this.refresh();
  }
}
