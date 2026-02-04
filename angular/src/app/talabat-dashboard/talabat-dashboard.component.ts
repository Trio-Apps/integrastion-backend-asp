import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { finalize } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TalabatDashboardService, TalabatDashboardDto, TalabatSyncLogItemDto, GetSyncLogsInput, TalabatVendorLookupDto } from '@proxy/talabat';
import { LocalizationModule, LocalizationService } from '@abp/ng.core';
import { FormsModule } from '@angular/forms';

import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';

@Component({
  selector: 'app-talabat-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    LocalizationModule, 
    FormsModule,
    TableModule,
    TagModule,
    TooltipModule,
    ButtonModule,
    SkeletonModule
  ],
  templateUrl: './talabat-dashboard.component.html',
  styleUrls: ['./talabat-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TalabatDashboardComponent implements OnInit {
  private readonly talabatDashboardService = inject(TalabatDashboardService);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly localization = inject(LocalizationService);

  private readonly dashboardSignal = signal<TalabatDashboardDto | null>(null);
  readonly dashboard = computed(() => this.dashboardSignal());

  readonly loading = signal<boolean>(false);
  readonly branchLoading = signal<boolean>(false);
  readonly tableLoading = signal<boolean>(false);
  
  readonly vendorCode = signal<string>(''); // empty = Host mode (all vendors)
  readonly vendors = signal<TalabatVendorLookupDto[]>([]);
  readonly vendorsLoading = signal<boolean>(false);
  readonly statusDialogVisible = signal<boolean>(false);
  readonly selectedStatus = signal<string>('CLOSED_UNTIL');
  readonly statusOptions = [
    { label: 'OPEN', value: 'OPEN' },
    { label: 'CLOSED', value: 'CLOSED' },
    { label: 'CLOSED_UNTIL', value: 'CLOSED_UNTIL' },
    { label: 'CLOSED_TODAY', value: 'CLOSED_TODAY' },
    { label: 'INACTIVE', value: 'INACTIVE' },
    { label: 'UNKNOWN', value: 'UNKNOWN' },
  ];
  // Pagination for sync logs table
  readonly syncLogs = signal<TalabatSyncLogItemDto[]>([]);
  readonly totalRecords = signal<number>(0);
  readonly rows = signal<number>(10);
  readonly first = signal<number>(0);
  
  readonly syncMetrics = computed(() => {
    const dashboard = this.dashboardSignal();
    if (!dashboard) {
      return [];
    }

    const counts = dashboard.counts;
    return [
      { label: '::TalabatDashboard.TotalSubmissions', value: counts.totalSubmissions, tone: 'primary', icon: 'pi-send' },
      { label: '::TalabatDashboard.Successful', value: counts.successfulSubmissions, tone: 'success', icon: 'pi-check-circle' },
      { label: '::TalabatDashboard.Failed', value: counts.failedSubmissions, tone: 'danger', icon: 'pi-times-circle' },
      { label: '::TalabatDashboard.Pending', value: counts.pendingSubmissions, tone: 'warning', icon: 'pi-clock' },
    ];
  });

  readonly productMetrics = computed(() => {
    const dashboard = this.dashboardSignal();
    if (!dashboard) {
      return [];
    }

    const stats = dashboard.stagingStats;
    return [
      { label: '::TalabatDashboard.TotalProducts', value: stats.totalProducts, tone: 'info', icon: 'pi-box' },
      { label: '::TalabatDashboard.ActiveProducts', value: stats.activeProducts, tone: 'success', icon: 'pi-check' },
      { label: '::TalabatDashboard.Synced', value: stats.completedProducts, tone: 'primary', icon: 'pi-sync' },
      { label: '::TalabatDashboard.FailedProducts', value: stats.failedProducts, tone: 'danger', icon: 'pi-exclamation-triangle' },
    ];
  });

  readonly branchStatus = computed(() => this.dashboardSignal()?.branchStatus);
  readonly stagingStats = computed(() => this.dashboardSignal()?.stagingStats);

  ngOnInit(): void {
    this.loadVendors();
  }

  private loadVendors(): void {
    this.vendorsLoading.set(true);

    this.talabatDashboardService
      .getVendors()
      .pipe(finalize(() => this.vendorsLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: vendors => {
          this.vendors.set(vendors ?? []);

          // If vendorCode is empty (host mode), keep it empty.
          // If you want a default vendor for tenant users, uncomment the block below.
          // if (!this.vendorCode() && vendors?.length) {
          //   this.vendorCode.set(vendors[0].vendorCode);
          // }

          this.refresh();
        },
        error: error => {
          console.error('Failed to load Talabat vendors', error);
          this.vendors.set([]);
          this.refresh(); // still load dashboard in host mode
        },
      });
  }

  onVendorChange(value: string): void {
    this.vendorCode.set(value ?? '');
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);

    // Pass undefined if vendorCode is empty (Host mode shows all data)
    const vendorCodeParam = this.vendorCode()?.trim() || undefined;

    this.talabatDashboardService
      .getDashboard(vendorCodeParam)
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: dashboard => {
          this.dashboardSignal.set(dashboard);
          // Load sync logs with pagination
          this.loadSyncLogs({ first: 0, rows: this.rows() });
        },
        error: error => {
          console.error('Failed to load Talabat dashboard data', error);
          this.messageService.add({
            severity: 'error',
            summary: this.l('::TalabatDashboard.Toast.Error'),
            detail: this.l('::TalabatDashboard.Toast.LoadError'),
          });
        },
      });
  }

  loadSyncLogs(event?: TableLazyLoadEvent): void {
    this.tableLoading.set(true);

    const skipCount = event?.first ?? 0;
    const maxResultCount = event?.rows ?? this.rows();

    let sorting = '';
    if (event?.sortField) {
      sorting = `${event.sortField} ${event.sortOrder === 1 ? 'asc' : 'desc'}`;
    }

    // Pass undefined if vendorCode is empty (Host mode shows all data)
    const vendorCodeParam = this.vendorCode()?.trim() || undefined;

    const input: GetSyncLogsInput = {
      vendorCode: vendorCodeParam,
      skipCount,
      maxResultCount,
      sorting: sorting || undefined
    };

    this.talabatDashboardService
      .getSyncLogs(input)
      .pipe(
        finalize(() => this.tableLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: result => {
          this.syncLogs.set(result.items || []);
          this.totalRecords.set(result.totalCount || 0);
          this.first.set(skipCount);
        },
        error: error => {
          console.error('Failed to load sync logs', error);
          this.messageService.add({
            severity: 'error',
            summary: this.l('::TalabatDashboard.Toast.Error'),
            detail: this.l('::TalabatDashboard.Toast.LoadError'),
          });
        },
      });
  }

  setBranchBusy(): void {
    const vendor = this.vendorCode()?.trim();
    
    if (!vendor) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Validation Error',
        detail: 'Please enter a vendor code',
      });
      return;
    }

    this.branchLoading.set(true);
    
    this.talabatDashboardService
      .setBranchBusy(vendor, 'Temporarily busy', 30)
      .pipe(finalize(() => this.branchLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.l('::TalabatDashboard.Toast.Success'),
            detail: this.l('::TalabatDashboard.Toast.BusySuccess'),
          });
          this.refresh();
        },
        error: error => {
          console.error('Failed to set branch busy', error);
          this.messageService.add({
            severity: 'error',
            summary: this.l('::TalabatDashboard.Toast.Error'),
            detail: this.l('::TalabatDashboard.Toast.BusyError'),
          });
        },
      });
  }

  setBranchAvailable(): void {
    const vendor = this.vendorCode()?.trim();
    
    if (!vendor) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Validation Error',
        detail: 'Please enter a vendor code',
      });
      return;
    }

    this.branchLoading.set(true);
    
    this.talabatDashboardService
      .setBranchAvailable(vendor)
      .pipe(finalize(() => this.branchLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.l('::TalabatDashboard.Toast.Success'),
            detail: this.l('::TalabatDashboard.Toast.AvailableSuccess'),
          });
          this.refresh();
        },
        error: error => {
          console.error('Failed to set branch available', error);
          this.messageService.add({
            severity: 'error',
            summary: this.l('::TalabatDashboard.Toast.Error'),
            detail: this.l('::TalabatDashboard.Toast.AvailableError'),
          });
        },
      });
  }

  openStatusDialog(): void {
    this.statusDialogVisible.set(true);
  }

  closeStatusDialog(): void {
    this.statusDialogVisible.set(false);
  }

  applyStatus(): void {
    const status = this.selectedStatus();
    // TODO: Real implementation should call a unified API to set any status.
    // For now, we hardcode:
    // - OPEN   -> setBranchAvailable()
    // - other  -> setBranchBusy() with default reason/closingMinutes
    if (status === 'OPEN') {
      this.setBranchAvailable();
    } else {
      // Hardcoded path: reuse busy endpoint for all non-OPEN statuses.
      this.setBranchBusy();
    }
    this.closeStatusDialog();
  }

  getStatusSeverity(status: string | undefined): 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast' | undefined {
    switch (status?.toLowerCase()) {
      case 'success':
      case 'done':
        return 'success';
      case 'failed':
        return 'danger';
      case 'submitted':
      case 'processing':
      case 'in_progress':
        return 'warn';
      default:
        return 'secondary';
    }
  }

  getStatusIcon(status: string | undefined): string {
    switch (status?.toLowerCase()) {
      case 'success':
      case 'done':
        return 'pi pi-check-circle';
      case 'failed':
        return 'pi pi-times-circle';
      case 'submitted':
      case 'processing':
      case 'in_progress':
        return 'pi pi-spin pi-spinner';
      default:
        return 'pi pi-question-circle';
    }
  }

  formatDate(date: string | undefined): string {
    if (!date) return '-';
    return new Date(date).toLocaleString();
  }

  formatDuration(seconds: number | undefined): string {
    if (!seconds) return '-';
    if (seconds < 60) return `${seconds}s`;
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return `${minutes}m ${remainingSeconds}s`;
  }

  private l(key: string, ...interpolateParams: string[]): string {
    return this.localization.instant(key, ...interpolateParams);
  }
}
