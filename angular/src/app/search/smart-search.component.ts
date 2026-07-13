import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SmartSearchService, SmartSearchResultDto } from './smart-search.service';

@Component({
  selector: 'app-smart-search',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './smart-search.component.html',
  styleUrls: ['./smart-search.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SmartSearchComponent {
  private readonly svc = inject(SmartSearchService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly query$ = new Subject<string>();

  readonly keyword = signal('');
  readonly result = signal<SmartSearchResultDto | null>(null);
  readonly loading = signal(false);

  constructor() {
    this.query$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
        switchMap(k => {
          const term = k.trim();
          if (term.length < 2) {
            this.result.set(null);
            this.loading.set(false);
            return of(null);
          }
          this.loading.set(true);
          return this.svc.search(term);
        }),
      )
      .subscribe({
        next: r => {
          this.loading.set(false);
          if (r) {
            this.result.set(r);
          }
        },
        error: () => this.loading.set(false),
      });
  }

  onInput(value: string): void {
    this.keyword.set(value);
    this.query$.next(value);
  }

  statusClass(status?: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'succeeded' || s === 'completed') return 'ok';
    if (s === 'failed') return 'fail';
    if (s === 'processing') return 'proc';
    return 'wait';
  }
}
