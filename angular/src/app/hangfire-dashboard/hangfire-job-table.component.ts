import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { HangfireJobItemDto } from '@proxy/background-jobs';
import { LocalizationModule } from '@abp/ng.core';

@Component({
  selector: 'app-hangfire-job-table',
  standalone: true,
  imports: [CommonModule, DatePipe, LocalizationModule],
  templateUrl: './hangfire-job-table.component.html',
  styleUrls: ['./hangfire-job-table.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HangfireJobTableComponent {
  @Input({ required: true }) title!: string;
  @Input({ required: true }) jobs: HangfireJobItemDto[] = [];
  @Input({ required: true }) emptyState!: string;

  getBadgeClass(state?: string | null): string {
    switch ((state || '').toLowerCase()) {
      case 'succeeded':
        return 'badge bg-success-subtle text-success-emphasis border-success-subtle';
      case 'failed':
        return 'badge bg-danger-subtle text-danger-emphasis border-danger-subtle';
      case 'processing':
        return 'badge bg-primary-subtle text-primary-emphasis border-primary-subtle';
      case 'scheduled':
        return 'badge bg-secondary-subtle text-secondary-emphasis border-secondary-subtle';
      case 'enqueued':
        return 'badge bg-info-subtle text-info-emphasis border-info-subtle';
      default:
        return 'badge bg-light text-dark border';
    }
  }

  // Turn a raw Hangfire method (e.g. "MenuSyncRecurringJob.ExecuteAsync") into a human label.
  displayName(job: HangfireJobItemDto): string {
    const raw = job.jobName || '';
    const l = raw.toLowerCase();
    if (l.includes('menusync')) return 'Menu synchronization';
    if (l.includes('catalog')) return 'Catalog submission';
    if (l.includes('dispatch')) return 'Order dispatch';
    if (l.includes('availability')) return 'Availability update';
    const cls = raw.split('.')[0].split('+').pop() || raw;
    return cls.replace(/(RecurringJob|Job)$/i, '').replace(/([a-z])([A-Z])/g, '$1 $2').trim() || raw;
  }

  // Plain-language error instead of a raw HTTP/stack message.
  friendlyError(job: HangfireJobItemDto): string {
    const e = job.exceptionMessage || '';
    if (!e) return '';
    if (e.includes('404')) return 'Endpoint not found (404)';
    if (e.includes('401') || e.includes('403')) return 'Authorization failed';
    if (e.includes('429')) return 'Rate limited by Talabat';
    if (/timed? ?out|timeout/i.test(e)) return 'Request timed out';
    return e.length > 90 ? e.slice(0, 90) + '…' : e;
  }

  trackByJobId(_: number, job: HangfireJobItemDto): string {
    return job.id;
  }
}

