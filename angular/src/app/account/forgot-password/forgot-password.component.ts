import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { RestService } from '@abp/ng.core';
import { finalize } from 'rxjs/operators';

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

    const returnUrl = `${window.location.origin}/reset-password`;

    this.loading = true;
    this.restService
      .request<
        { tenantName?: string; email: string; returnUrl?: string },
        void
      >(
        {
          method: 'POST',
          url: '/api/account/password/forgot',
          body: {
            tenantName: tenantName || undefined,
            email,
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
  }

  backToLogin(): void {
    this.router.navigateByUrl('/account/login');
  }
}
