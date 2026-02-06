import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LocalizationModule } from '@abp/ng.core';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TalabatOrderLogsService, TalabatOrderLogDto, GetTalabatOrderLogsInput } from '@proxy/talabat';

@Component({
  selector: 'app-talabat-orders',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationModule,
    TableModule,
    TagModule,
    ButtonModule,
    InputTextModule,
  ],
  templateUrl: './talabat-orders.component.html',
  styleUrls: ['./talabat-orders.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TalabatOrdersComponent {
  private readonly orderLogsService = inject(TalabatOrderLogsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal<boolean>(false);
  readonly logs = signal<TalabatOrderLogDto[]>([]);
  readonly totalRecords = signal<number>(0);
  readonly rows = signal<number>(10);
  readonly first = signal<number>(0);

  readonly vendorCode = signal<string>('');
  readonly status = signal<string>('');

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
      vendorCode: this.vendorCode().trim() || undefined,
      status: this.status() || undefined,
    };

    this.orderLogsService
      .getList(input)
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.logs.set(result.items || []);
          this.totalRecords.set(result.totalCount || 0);
          this.first.set(skipCount);
        },
        error: error => {
          console.error('Failed to load Talabat order logs', error);
          this.logs.set([]);
          this.totalRecords.set(0);
        },
      });
  }

  getStatusSeverity(status?: string): 'success' | 'warning' | 'danger' | 'info' | 'secondary' {
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
        return 'warning';
      default:
        return 'secondary';
    }
  }

  getOrderCode(log: TalabatOrderLogDto): string {
    return log.shortCode || log.orderCode || log.orderToken || '-';
  }
}
