import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MultiTenancyService, RestService, SessionStateService } from '@abp/ng.core';
import { finalize } from 'rxjs/operators';
import { Observable, of } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { PasswordModule } from 'primeng/password';
import { InputTextModule } from 'primeng/inputtext';
import { CardModule } from 'primeng/card';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    PasswordModule,
    InputTextModule,
    CardModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './reset-password.component.html',
})
export class ResetPasswordComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly restService = inject(RestService);
  private readonly multiTenancy = inject(MultiTenancyService);
  private readonly sessionState = inject(SessionStateService);
  private readonly messageService = inject(MessageService);

  loading = false;
  tokenVerified = false;

  readonly form = this.fb.group({
    tenantName: [''],
    userId: ['', [Validators.required]],
    resetToken: ['', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(6)]],
  });

  ngOnInit(): void {
    const tenantName = (this.route.snapshot.queryParamMap.get('tenantName') ?? '').trim();
    const userId = (this.route.snapshot.queryParamMap.get('userId') ?? '').trim();
    const resetToken = (this.route.snapshot.queryParamMap.get('resetToken') ?? '').trim();

    this.form.patchValue({
      tenantName,
      userId,
      resetToken,
    });

    if (userId && resetToken) {
      this.verifyToken();
    }
  }

  verifyToken(): void {
    const tenantName = (this.form.controls.tenantName.value ?? '').trim();
    const userId = (this.form.controls.userId.value ?? '').trim();
    const resetToken = (this.form.controls.resetToken.value ?? '').trim();

    if (!userId || !resetToken) {
      return;
    }

    this.loading = true;
    this.ensureTenantContext(tenantName).subscribe({
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

        this.restService
          .request<{ userId: string; resetToken: string }, void>(
            {
              method: 'POST',
              url: '/api/account/verify-password-reset-token',
              body: { userId, resetToken },
            },
            { apiName: 'default' }
          )
          .pipe(finalize(() => (this.loading = false)))
          .subscribe({
            next: () => {
              this.tokenVerified = true;
            },
            error: err => {
              this.tokenVerified = false;
              this.messageService.add({
                severity: 'error',
                summary: 'Invalid Link',
                detail: err?.error?.error?.message || 'Reset link is invalid or expired.',
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

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const tenantName = (this.form.controls.tenantName.value ?? '').trim();
    const userId = (this.form.controls.userId.value ?? '').trim();
    const resetToken = (this.form.controls.resetToken.value ?? '').trim();
    const password = this.form.controls.password.value ?? '';
    const confirmPassword = this.form.controls.confirmPassword.value ?? '';

    if (password !== confirmPassword) {
      this.messageService.add({
        severity: 'error',
        summary: 'Validation',
        detail: 'Password and confirmation do not match.',
      });
      return;
    }

    this.loading = true;
    this.ensureTenantContext(tenantName).subscribe({
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

        this.restService
          .request<{ userId: string; resetToken: string; password: string }, void>(
            {
              method: 'POST',
              url: '/api/account/reset-password',
              body: {
                userId,
                resetToken,
                password,
              },
            },
            { apiName: 'default' }
          )
          .pipe(finalize(() => (this.loading = false)))
          .subscribe({
            next: () => {
              this.messageService.add({
                severity: 'success',
                summary: 'Success',
                detail: 'Password has been reset successfully.',
              });
              this.router.navigateByUrl('/account/login');
            },
            error: err => {
              this.messageService.add({
                severity: 'error',
                summary: 'Error',
                detail: err?.error?.error?.message || 'Failed to reset password.',
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

  private ensureTenantContext(tenantName: string): Observable<{ success: boolean }> {
    if (tenantName) {
      return this.multiTenancy.setTenantByName(tenantName);
    }

    this.sessionState.setTenant(null);
    return of({ success: true });
  }
}
