import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RestService } from '@abp/ng.core';
import { DynamicDialogRef, DynamicDialogConfig } from 'primeng/dynamicdialog';

interface ResetResult {
  password: string;
  generated: boolean;
}

/**
 * Administrator-side password reset. The new password is shown once, here, so it can be handed
 * over; it is never stored in clear and cannot be read again afterwards.
 */
@Component({
  selector: 'app-reset-password-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp">
      @if (result(); as r) {
        <p class="rp-note">
          Password reset for <strong>{{ userName }}</strong>. Give them this password — it cannot
          be shown again, and they will be asked to change it when they sign in.
        </p>
        <div class="rp-out">
          <code>{{ r.password }}</code>
          <button type="button" class="rp-btn" (click)="copy(r.password)">
            {{ copied() ? 'Copied' : 'Copy' }}
          </button>
        </div>
        <div class="rp-actions">
          <button type="button" class="rp-btn primary" (click)="close()">Done</button>
        </div>
      } @else {
        <p class="rp-note">
          Set a password for <strong>{{ userName }}</strong>, or leave it empty to have one
          generated. They will be asked to change it when they sign in.
        </p>
        <label class="rp-field">
          New password <span class="rp-muted">(optional)</span>
          <input type="text" [(ngModel)]="password" placeholder="Leave empty to generate"
                 autocomplete="off" />
        </label>
        @if (error()) { <div class="rp-error">{{ error() }}</div> }
        <div class="rp-actions">
          <button type="button" class="rp-btn" (click)="close()" [disabled]="saving()">Cancel</button>
          <button type="button" class="rp-btn primary" (click)="submit()" [disabled]="saving()">
            {{ saving() ? 'Resetting…' : 'Reset password' }}
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { --brand: var(--primary-color, #276d64); display: block; }
    .rp { display: flex; flex-direction: column; gap: 14px; }
    .rp-note { margin: 0; font-size: 0.88rem; line-height: 1.6; color: var(--text-color-secondary); }
    .rp-note strong { color: var(--text-color); }
    .rp-field { display: flex; flex-direction: column; gap: 6px; font-size: 0.8rem; color: var(--text-color-secondary); }
    .rp-field input {
      border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.5rem 0.7rem;
      font-size: 0.9rem; color: var(--text-color); background: var(--surface-card);
      &:focus { outline: none; border-color: var(--brand); }
    }
    .rp-muted { opacity: 0.75; }
    .rp-out {
      display: flex; align-items: center; gap: 10px;
      border: 1px solid var(--brand); border-radius: 10px; padding: 0.6rem 0.8rem;
      background: color-mix(in srgb, var(--brand) 7%, transparent);
      code { flex: 1; font-family: ui-monospace, monospace; font-size: 1rem; color: var(--text-color); word-break: break-all; }
    }
    .rp-error { font-size: 0.85rem; color: #dc2626; }
    .rp-actions { display: flex; justify-content: flex-end; gap: 10px; }
    .rp-btn {
      border: 1px solid var(--surface-border); background: var(--surface-card); color: var(--text-color);
      border-radius: 10px; padding: 0.45rem 0.9rem; font-size: 0.85rem; cursor: pointer;
      &:hover:not(:disabled) { border-color: var(--brand); color: var(--brand); }
      &:disabled { opacity: 0.5; cursor: default; }
      &.primary { background: var(--brand); border-color: var(--brand); color: #fff;
        &:hover:not(:disabled) { color: #fff; filter: brightness(1.06); } }
    }
  `],
})
export class ResetPasswordModalComponent {
  private readonly rest = inject(RestService);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly dialogConfig = inject(DynamicDialogConfig);

  readonly userId: string = this.dialogConfig.data?.userId ?? '';
  readonly userName: string = this.dialogConfig.data?.userName ?? 'this user';

  password = '';
  readonly saving = signal(false);
  readonly copied = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<ResetResult | null>(null);

  submit(): void {
    if (!this.userId || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);

    this.rest
      .request<{ newPassword: string | null }, ResetResult>(
        {
          method: 'POST',
          url: `/api/app/user-password/${this.userId}/reset`,
          body: { newPassword: this.password.trim() || null },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: r => {
          this.result.set(r);
          this.saving.set(false);
        },
        error: err => {
          this.error.set(err?.error?.error?.message || 'Could not reset the password.');
          this.saving.set(false);
        },
      });
  }

  copy(value: string): void {
    navigator.clipboard?.writeText(value).then(
      () => this.copied.set(true),
      () => this.copied.set(false),
    );
  }

  close(): void {
    this.dialogRef.close(!!this.result());
  }
}
