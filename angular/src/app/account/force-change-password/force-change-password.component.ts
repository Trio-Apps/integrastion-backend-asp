import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { RestService } from '@abp/ng.core';
import { finalize } from 'rxjs/operators';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

@Component({
  selector: 'app-force-change-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    ButtonModule,
    PasswordModule,
    MessageModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './force-change-password.component.html',
})
export class ForceChangePasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly restService = inject(RestService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  loading = false;

  readonly form = this.fb.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmNewPassword: ['', [Validators.required, Validators.minLength(6)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const currentPassword = this.form.controls.currentPassword.value ?? '';
    const newPassword = this.form.controls.newPassword.value ?? '';
    const confirmNewPassword = this.form.controls.confirmNewPassword.value ?? '';

    if (newPassword !== confirmNewPassword) {
      this.messageService.add({
        severity: 'error',
        summary: 'Validation',
        detail: 'New password and confirmation do not match.',
      });
      return;
    }

    this.loading = true;
    const input: ChangePasswordRequest = {
      currentPassword,
      newPassword,
      confirmNewPassword,
    };

    this.restService
      .request<ChangePasswordRequest, void>(
        {
          method: 'POST',
          url: '/api/account/password/change',
          body: input,
        },
        { apiName: 'Default' }
      )
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Success',
            detail: 'Password changed successfully.',
          });
          this.router.navigateByUrl('/');
        },
        error: error => {
          this.messageService.add({
            severity: 'error',
            summary: 'Error',
            detail: error?.error?.error?.message || 'Failed to change password.',
          });
        },
      });
  }
}
