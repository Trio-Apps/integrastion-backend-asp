import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { OrderDetailService, TalabatOrderDetailsDto } from './order-detail.service';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './order-detail.component.html',
  styleUrls: ['./order-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetailComponent implements OnInit {
  private readonly svc = inject(OrderDetailService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly order = signal<TalabatOrderDetailsDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly showPayload = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.load(id);
  }

  load(id: string): void {
    this.loading.set(true);
    this.error.set(false);
    this.svc
      .getDetails(id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: o => this.order.set(o),
        error: () => this.error.set(true),
      });
  }

  statusClass(status?: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'succeeded' || s === 'completed') return 'ok';
    if (s === 'failed') return 'fail';
    if (s === 'processing') return 'proc';
    return 'wait';
  }

  prettyPayload(raw?: string): string {
    if (!raw) return '';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  copyPayload(raw?: string): void {
    if (raw && navigator.clipboard) navigator.clipboard.writeText(this.prettyPayload(raw));
  }
}
