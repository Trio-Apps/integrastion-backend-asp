import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MultiTenancyService, RestService, SessionStateService } from '@abp/ng.core';
import { finalize } from 'rxjs/operators';
import { Observable, of } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CardModule } from 'primeng/card';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, ButtonModule, InputTextModule, CardModule, ToastModule],
  providers: [MessageService],
  templateUrl: './forgot-password.component.html',
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly restService = inject(RestService);
  private readonly multiTenancy = inject(MultiTenancyService);
  private readonly sessionState = inject(SessionStateService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  loading = false;

  readonly form = this.fb.group({
    tenantName: [''],
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const tenantName = (this.form.controls.tenantName.value ?? '').trim();
    const email = (this.form.controls.email.value ?? '').trim();

    this.loading = true;

    const run = tenantName
      ? this.multiTenancy.setTenantByName(tenantName)
      : this.clearTenantContextAndContinue();

    run.subscribe({
      next: tenantResult => {
        if (tenantName && !tenantResult?.success) {
          this.loading = false;
          this.messageService.add({
            severity: 'error',
            summary: 'Tenant Not Found',
            detail: 'The specified tenant was not found.',
          });
          return;
        }

        const returnUrl = tenantName
          ? `${window.location.origin}/reset-password?tenantName=${encodeURIComponent(tenantName)}`
          : `${window.location.origin}/reset-password`;

        this.restService
          .request<
            { email: string; appName: string; returnUrl: string; returnUrlHash?: string },
            void
          >(
            {
              method: 'POST',
              url: '/api/account/send-password-reset-code',
              body: {
                email,
                appName: 'Angular',
                returnUrl,
              },
            },
            { apiName: 'Default' }
          )
          .pipe(finalize(() => (this.loading = false)))
          .subscribe({
            next: () => {
              this.messageService.add({
                severity: 'success',
                summary: 'Success',
                detail: 'If the email exists, a reset link has been sent.',
              });
            },
            error: err => {
              this.messageService.add({
                severity: 'error',
                summary: 'Error',
                detail: err?.error?.error?.message || 'Failed to send reset link.',
              });
            },
          });
      },
      error: () => {
        this.loading = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to resolve tenant context.',
        });
      },
    });
  }

  backToLogin(): void {
    this.router.navigateByUrl('/account/login');
  }

  private clearTenantContextAndContinue(): Observable<{ success: boolean }> {
    this.sessionState.setTenant(null);
    return of({ success: true });
  }
}
