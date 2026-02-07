import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService, CoreModule, LocalizationPipe, MultiTenancyService, SessionStateService } from '@abp/ng.core';
import { finalize } from 'rxjs/operators';

// PrimeNG Imports
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LocalizationPipe,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    CheckboxModule,
    ToastModule,
    CoreModule
  ],
  providers: [MessageService],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private messageService = inject(MessageService);
  private sessionState = inject(SessionStateService);
  private multiTenancy = inject(MultiTenancyService);
  loginForm!: FormGroup;
  loading = false;
  submitted = false;
  passwordVisible = false;

  ngOnInit(): void {
    this.initializeForm();
  }

  /**
   * Initializes the login form with validators
   */
  private initializeForm(): void {
    this.loginForm = this.fb.group({
      tenantName: [''],
      username: ['', [Validators.required, Validators.minLength(3)]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      rememberMe: [false]
    });
  }

  /**
   * Getter for easy access to form controls
   */
  get f() {
    return this.loginForm.controls;
  }

  /**
   * Handles form submission
   */
  onSubmit(): void {
    this.submitted = true;

    // Stop if form is invalid
    if (this.loginForm.invalid) {
      this.showValidationErrors();
      return;
    }

    this.loading = true;
    const { tenantName, username, password, rememberMe } = this.loginForm.value;
    const tenantNameValue = (tenantName ?? '').toString().trim();

    // If tenant name is provided, validate it first
    if (tenantNameValue) {
      // Use ABP MultiTenancyService so it refreshes app state correctly after setting tenant
      this.multiTenancy.setTenantByName(tenantNameValue).subscribe({
        next: (data) => {
          if (data.success) {
            this.performLogin(username, password, rememberMe);
          } else {
            this.loading = false;
            this.messageService.add({
              severity: 'error',
              summary: 'Tenant Not Found',
              detail: 'The specified tenant name was not found. Please check and try again.'
            });
          }
        },
        error: (error) => {
          console.error('Tenant lookup error:', error);
          this.loading = false;
          this.messageService.add({
            severity: 'error',
            summary: 'Error',
            detail: 'Failed to validate tenant name. Please try again.'
          });
        }
      });
    } else {
      this.clearTenantContext();
      this.performLogin(username, password, rememberMe);
    }
  }

  /**
   * Performs the actual login operation
   */
  private performLogin(username: string, password: string, rememberMe: boolean): void {
    this.authService
      .login({ username, password, rememberMe })
      .pipe(
        finalize(() => {
          this.loading = false;
        })
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Success',
            detail: 'Login successful! Redirecting...'
          });
          // Navigate to home or dashboard after successful login
          setTimeout(() => {
            this.router.navigate(['/']);
          }, 1000);
        },
        error: (error) => {
          console.error('Login error:', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Login Failed',
            detail: error?.error?.error_description || 'Invalid username or password'
          });
        }
      });
  }

  /**
   * Shows validation error messages
   */
  private showValidationErrors(): void {
    const errors: string[] = [];

    if (this.f['tenantName'].errors) {
      if (this.f['tenantName'].errors['required']) {
        errors.push('Tenant name is required');
      }
    }

    if (this.f['username'].errors) {
      if (this.f['username'].errors['required']) {
        errors.push('Username is required');
      } else if (this.f['username'].errors['minlength']) {
        errors.push('Username must be at least 3 characters');
      }
    }

    if (this.f['password'].errors) {
      if (this.f['password'].errors['required']) {
        errors.push('Password is required');
      } else if (this.f['password'].errors['minlength']) {
        errors.push('Password must be at least 6 characters');
      }
    }

    if (errors.length > 0) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Validation Error',
        detail: errors.join(', ')
      });
    }
  }

  private clearTenantContext(): void {
    // Ensure stale tenant cookie doesn't force tenant context for host logins.
    const cookieNames = ['__tenant', 'Abp.TenantId', 'AbpTenantId'];
    for (const name of cookieNames) {
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; domain=${location.hostname}`;
    }
    this.sessionState.setTenant(null);
  }

  /**
   * Toggles password visibility
   */
  togglePasswordVisibility(): void {
    this.passwordVisible = !this.passwordVisible;
  }

  /**
   * Navigates to forgot password page
   */
  onForgotPassword(): void {
    // Uncomment when route is ready
    // this.router.navigate(['/forgot-password']);
    this.messageService.add({
      severity: 'info',
      summary: 'Info',
      detail: 'Forgot password functionality coming soon'
    });
  }
}
