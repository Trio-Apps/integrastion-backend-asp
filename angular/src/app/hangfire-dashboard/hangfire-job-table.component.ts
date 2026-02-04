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

  trackByJobId(_: number, job: HangfireJobItemDto): string {
    return job.id;
  }
}

