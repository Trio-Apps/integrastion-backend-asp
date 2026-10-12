import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService, PermissionService } from '@abp/ng.core';
import { Router } from '@angular/router';

/**
 * Shown when a signed-in user has no page they are allowed to open. Without it ABP's
 * permissionGuard simply never resolves and the browser ends up on the API's error page.
 */
@Component({
  selector: 'app-no-access',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="na-wrap">
      <div class="na-card">
        <i class="pi pi-lock"></i>
        <h1>Your account has no access yet</h1>
        <p>
          You are signed in, but no permissions have been granted to your roles, so there is
          nothing to show. Ask an administrator to assign you a role.
        </p>
        <div class="na-actions">
          <button type="button" class="na-btn" (click)="retry()">Check again</button>
          <button type="button" class="na-btn primary" (click)="logout()">Sign out</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { --brand: var(--primary-color, #276d64); display: block; }
    .na-wrap { min-height: 60vh; display: flex; align-items: center; justify-content: center; padding: 24px; }
    .na-card {
      max-width: 460px; text-align: center;
      display: flex; flex-direction: column; align-items: center; gap: 12px;
      background: var(--surface-card); border: 1px solid var(--surface-border);
      border-radius: 14px; padding: 2rem 1.75rem;
    }
    i { font-size: 2rem; color: var(--brand); }
    h1 { margin: 0; font-size: 1.2rem; font-weight: 600; color: var(--text-color); }
    p { margin: 0; font-size: 0.9rem; line-height: 1.6; color: var(--text-color-secondary); }
    .na-actions { display: flex; gap: 10px; margin-top: 6px; }
    .na-btn {
      border: 1px solid var(--surface-border); background: var(--surface-card);
      color: var(--text-color); border-radius: 10px; padding: 0.5rem 1rem;
      font-size: 0.88rem; cursor: pointer;
      &:hover { border-color: var(--brand); color: var(--brand); }
    }
    .na-btn.primary { background: var(--brand); border-color: var(--brand); color: #fff; }
  `],
})
export class NoAccessComponent {
  private readonly auth = inject(AuthService);
  private readonly permission = inject(PermissionService);
  private readonly router = inject(Router);

  /** Permissions may have been granted since sign-in. */
  retry(): void {
    if (this.permission.getGrantedPolicy(LANDING_POLICY)) {
      this.router.navigateByUrl('/dashboard');
    } else {
      window.location.reload();
    }
  }

  logout(): void {
    this.auth.logout().subscribe();
  }
}

/** The policy that decides whether the default landing page is reachable. */
export const LANDING_POLICY = 'OrderXChange.Dashboard.Host || OrderXChange.Dashboard.Tenant';
