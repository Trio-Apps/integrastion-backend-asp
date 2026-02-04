import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { MessageService } from 'primeng/api';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HangfireMonitoringService, HangfireDashboardDto } from '@proxy/background-jobs';
import { HangfireJobTableComponent } from './hangfire-job-table.component';
import { LocalizationService } from '@abp/ng.core';
import { LocalizationModule } from '@abp/ng.core';

@Component({
  selector: 'app-hangfire-dashboard',
  standalone: true,
  imports: [CommonModule, HangfireJobTableComponent, LocalizationModule, FormsModule],
  templateUrl: './hangfire-dashboard.component.html',
  styleUrls: ['./hangfire-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HangfireDashboardComponent implements OnInit {
  private readonly hangfireMonitoringService = inject(HangfireMonitoringService);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly localization = inject(LocalizationService);

  private readonly dashboardSignal = signal<HangfireDashboardDto | null>(null);
  readonly dashboard = computed(() => this.dashboardSignal());

  readonly loading = signal<boolean>(false);
  readonly hangfireUrl = 'https://localhost:44325/hangfire';
  readonly cronExpression = signal<string>('');
  readonly metrics = computed(() => {
    const dashboard = this.dashboardSignal();
    if (!dashboard) {
      return [];
    }

    const counts = dashboard.counts;
    return [
      { label: this.l('::MenuSync.Metrics.Succeeded'), value: counts.succeeded ?? 0, tone: 'success' },
      { label: this.l('::MenuSync.Metrics.Failed'), value: counts.failed ?? 0, tone: 'danger' },
      { label: this.l('::MenuSync.Metrics.Processing'), value: counts.processing ?? 0, tone: 'primary' },
      { label: this.l('::MenuSync.Metrics.Deleted'), value: counts.deleted ?? 0, tone: 'dark' },
    ];
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);

    this.hangfireMonitoringService
      .getDashboard()
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: dashboard => {
          this.dashboardSignal.set(dashboard);
        },
        error: error => {
          console.error('Failed to load menu sync dashboard data', error);
          this.messageService.add({
            severity: 'error',
            summary: this.l('::MenuSync.Toast.Title'),
            detail: this.l('::MenuSync.Toast.LoadError'),
          });
        },
      });
  }

  private l(key: string, ...interpolateParams: string[]): string {
    return this.localization.instant(key, ...interpolateParams);
  }
}

