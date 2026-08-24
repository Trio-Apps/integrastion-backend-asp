import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DashboardService, DashboardOverviewDto, DashboardOrderCountDto } from '../dashboard/dashboard.service';

@Component({
  selector: 'app-talabat-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './talabat-dashboard.component.html',
  styleUrls: ['./talabat-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TalabatDashboardComponent implements OnInit {
  private readonly svc = inject(DashboardService);
  private readonly destroyRef = inject(DestroyRef);

  readonly data = signal<DashboardOverviewDto | null>(null);

  // Order counter for a chosen day — defaults to today.
  readonly countDate = signal<string>(new Date().toLocaleDateString('en-CA'));
  readonly dayCount = signal<DashboardOrderCountDto | null>(null);
  readonly countLoading = signal<boolean>(false);
  readonly today = new Date().toLocaleDateString('en-CA');
  readonly loading = signal<boolean>(false);
  readonly error = signal<boolean>(false);

  readonly maxTrend = computed(() => {
    const trend = this.data()?.ordersTrend ?? [];
    return Math.max(1, ...trend.map(d => d.count));
  });

  readonly statusBreakdown = computed(() => {
    const o = this.data()?.orders;
    if (!o) return [];
    const total = o.succeeded + o.processing + o.enqueued + o.failed;
    const seg = (label: string, count: number, color: string) => ({
      label,
      count,
      color,
      pct: total > 0 ? (count / total) * 100 : 0,
    });
    return [
      seg('Succeeded', o.succeeded, '#10b981'),
      seg('Processing', o.processing, '#1d9e75'),
      seg('Enqueued', o.enqueued, '#f59e0b'),
      seg('Failed', o.failed, '#ef4444'),
    ];
  });

  ngOnInit(): void {
    this.load();
    this.loadDayCount();
  }

  onCountDateChange(value: string): void {
    if (!value) return;
    this.countDate.set(value);
    this.loadDayCount();
  }

  loadDayCount(): void {
    this.countLoading.set(true);
    this.svc
      .getOrderCount(this.countDate())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.countLoading.set(false)),
      )
      .subscribe({
        next: c => this.dayCount.set(c),
        error: () => this.dayCount.set(null),
      });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.svc
      .getOverview()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: d => this.data.set(d),
        error: () => this.error.set(true),
      });
  }

  barHeight(count: number): number {
    return Math.max(4, Math.round((count / this.maxTrend()) * 100));
  }

  statusClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'succeeded' || s === 'completed') return 'ok';
    if (s === 'failed') return 'fail';
    if (s === 'processing') return 'proc';
    return 'wait';
  }
}
