import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { PasswordModule } from 'primeng/password';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { SmtpConfigService } from '../../proxy/smtp-config/smtp-config.service';

@Component({
  selector: 'app-smtp-config',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    InputNumberModule,
    PasswordModule,
    CheckboxModule,
    ButtonModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './smtp-config.component.html',
  styleUrl: './smtp-config.component.scss',
})
export class SmtpConfigComponent implements OnInit {
  private fb = inject(FormBuilder);
  private messageService = inject(MessageService);
  private smtpConfigService = inject(SmtpConfigService);

  form: FormGroup = this.fb.group({
    host: ['', [Validators.required]],
    port: [587, [Validators.required, Validators.min(1), Validators.max(65535)]],
    userName: ['', [Validators.required]],
    password: ['', [Validators.required]],
    fromName: ['', [Validators.required]],
    fromEmail: ['', [Validators.required, Validators.email]],
    enableSsl: [true],
    useStartTls: [true],
  });

  saving = false;
  loading = false;
  testing = false;

  ngOnInit(): void {
    this.loading = true;
    this.smtpConfigService.get().subscribe({
      next: (config) => {
        if (config) {
          this.form.patchValue({
            host: config.host ?? '',
            port: config.port ?? 587,
            userName: config.userName ?? '',
            password: config.password ?? '',
            fromName: config.fromName ?? '',
            fromEmail: config.fromEmail ?? '',
            enableSsl: config.enableSsl ?? true,
            useStartTls: config.useStartTls ?? true,
          });
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to load SMTP settings.',
          life: 3000,
        });
      },
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.smtpConfigService.save(this.form.value).subscribe({
      next: () => {
        this.saving = false;
        this.messageService.add({
          severity: 'success',
          summary: 'Saved',
          detail: 'SMTP settings saved.',
          life: 2500,
        });
      },
      error: () => {
        this.saving = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to save SMTP settings.',
          life: 3000,
        });
      },
    });
  }

  testConnection(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.testing = true;
    this.smtpConfigService.test(this.form.value).subscribe({
      next: (result) => {
        this.testing = false;
        if (result?.success) {
          this.messageService.add({
            severity: 'success',
            summary: 'Success',
            detail: result.message ?? 'SMTP connection test succeeded.',
            life: 2500,
          });
        } else {
          this.messageService.add({
            severity: 'error',
            summary: 'Error',
            detail: result?.message ?? 'SMTP connection test failed.',
            life: 3500,
          });
        }
      },
      error: () => {
        this.testing = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to test SMTP connection.',
          life: 3500,
        });
      },
    });
  }
}
