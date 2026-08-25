import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FailedOrdersReportService } from './failed-orders-report.service';

@Component({
  selector: 'app-failed-orders-report',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fr-page">
      <div class="fr-head">
        <h1>Daily Failed Orders Report</h1>
        <p>
          Get an email at the end of each day listing orders that stayed <strong>Failed</strong>
          after all {{ 3 }} retry attempts. Leave the email empty to turn the report off.
        </p>
      </div>

      <div class="fr-card">
        <div class="fr-fields">
          <label class="grow">Report email
            <input type="email" [ngModel]="email()" (ngModelChange)="email.set($event)" placeholder="ops@yourcompany.com" />
          </label>
          <label>Send at
            <input type="time" [ngModel]="time()" (ngModelChange)="time.set($event)" />
          </label>
        </div>
        <div class="fr-tz muted">Time is in your timezone ({{ timeZone() }}).</div>

        <div class="fr-actions">
          <button class="btn primary" [disabled]="saving()" (click)="save()">{{ saving() ? 'Saving…' : 'Save' }}</button>
          <button class="btn" [disabled]="testing() || !email().trim()" (click)="sendTest()" title="Send today's report now to verify the setup">
            {{ testing() ? 'Sending…' : 'Send test now' }}
          </button>
        </div>
        <div class="fr-note muted">
          The report is sent once per day after the chosen time. It includes only orders received that day that
          remained failed after retries. If there are none, no email is sent (a test always sends).
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .fr-page { display: flex; flex-direction: column; gap: 14px; max-width: 720px; }
      .fr-head h1 { margin: 0; font-size: 1.4rem; font-weight: 600; color: var(--text-color); }
      .fr-head p { margin: 4px 0 0; font-size: 0.85rem; color: var(--text-color-secondary); }
      .fr-card { background: var(--surface-card); border: 1px solid var(--surface-border); border-radius: 12px; padding: 1.1rem 1.2rem; display: flex; flex-direction: column; gap: 12px; }
      .fr-fields { display: flex; flex-wrap: wrap; gap: 14px; }
      .fr-fields label { display: flex; flex-direction: column; gap: 5px; font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--text-color-secondary); }
      .fr-fields label.grow { flex: 1; min-width: 220px; }
      .fr-fields input { border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.55rem 0.7rem; font-size: 0.92rem; background: var(--surface-ground, #f8fafc); color: var(--text-color); }
      .fr-fields input:focus { outline: none; border-color: var(--primary-color, #276d64); }
      .fr-actions { display: flex; gap: 10px; }
      .btn { border: 1px solid var(--surface-border); background: var(--surface-card); color: var(--text-color); border-radius: 10px; padding: 0.55rem 1rem; font-size: 0.88rem; cursor: pointer; }
      .btn:hover:not(:disabled) { border-color: var(--primary-color, #276d64); color: var(--primary-color, #276d64); }
      .btn:disabled { opacity: 0.5; cursor: default; }
      .btn.primary { background: var(--primary-color, #276d64); border-color: var(--primary-color, #276d64); color: #fff; }
      .btn.primary:hover:not(:disabled) { filter: brightness(1.06); color: #fff; }
      .muted { font-size: 0.8rem; color: var(--text-color-secondary); }
    `,
  ],
})
export class FailedOrdersReportComponent implements OnInit {
  private readonly svc = inject(FailedOrdersReportService);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  readonly email = signal('');
  readonly time = signal('23:30');
  readonly timeZone = signal('Asia/Kuwait');
  readonly saving = signal(false);
  readonly testing = signal(false);

  ngOnInit(): void {
    this.svc
      .get()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: s => {
          this.email.set(s.email || '');
          this.time.set(s.time || '23:30');
          this.timeZone.set(s.timeZone || 'Asia/Kuwait');
        },
        error: () => {},
      });
  }

  save(): void {
    this.saving.set(true);
    this.svc
      .update({ email: this.email().trim(), time: this.time() })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false)))
      .subscribe({
        next: s => {
          this.email.set(s.email || '');
          this.time.set(s.time || '23:30');
          this.messageService.add({ severity: 'success', summary: 'Saved', detail: 'Report settings updated.' });
        },
        error: err =>
          this.messageService.add({
            severity: 'error',
            summary: 'Could not save',
            detail: err?.error?.error?.message || 'Please check the email and time (HH:mm).',
          }),
      });
  }

  sendTest(): void {
    this.testing.set(true);
    this.svc
      .sendTest()
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.testing.set(false)))
      .subscribe({
        next: r => this.messageService.add({ severity: 'success', summary: 'Test sent', detail: r.message }),
        error: err =>
          this.messageService.add({
            severity: 'error',
            summary: 'Test failed',
            detail: err?.error?.error?.message || 'Could not send the test email. Check SMTP config.',
          }),
      });
  }
}
