import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService, CoreModule, LocalizationPipe, MultiTenancyService, RestService, SessionStateService } from '@abp/ng.core';
import { finalize } from 'rxjs/operators';
import { firstValueFrom } from 'rxjs';
import { OAuthService } from 'angular-oauth2-oidc';

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
  private restService = inject(RestService);
  private oAuthService = inject(OAuthService);
  loginForm!: FormGroup;
  loading = false;
  submitted = false;
  passwordVisible = false;
  private tenantAuthValue: string | null = null;

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
            this.ensureTenantCookieForAuthentication(data, tenantNameValue)
              .catch(error => console.warn('Failed to persist tenant cookie for cross-subdomain auth', error))
              .finally(() => {
                this.setOAuthTenantContext(this.tenantAuthValue ?? tenantNameValue);
                this.resolveTenantLoginName(tenantNameValue, username)
                  .then(resolvedLogin => this.performLoginWithFallback(resolvedLogin, username, password, rememberMe, true))
                  .catch(() => this.performLoginWithFallback(username, username, password, rememberMe, true));
              });
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
      this.performLoginWithFallback(username, username, password, rememberMe, false);
    }
  }

  private async resolveTenantLoginName(tenantName: string, login: string): Promise<string> {
    const trimmedLogin = (login ?? '').trim();
    if (!tenantName || !trimmedLogin.includes('@')) {
      return trimmedLogin;
    }

    const encodedTenant = encodeURIComponent(tenantName.trim());
    const encodedLogin = encodeURIComponent(trimmedLogin);
    const url = `/api/account/login-resolver/username?tenantName=${encodedTenant}&login=${encodedLogin}`;

    const result = await firstValueFrom(
      this.restService.request<any, { login?: string }>(
        {
          method: 'GET',
          url,
        },
        { apiName: 'Default' }
      )
    );

    return (result?.login ?? trimmedLogin).trim();
  }

  /**
   * Performs the actual login operation
   */
  private performLoginWithFallback(
    primaryUsername: string,
    originalUsername: string,
    password: string,
    rememberMe: boolean,
    hasTenantContext: boolean
  ): void {
    const candidates = this.buildLoginCandidates(primaryUsername, originalUsername, hasTenantContext);
    this.tryLoginCandidate(candidates, password, rememberMe, hasTenantContext, 0);
  }

  private tryLoginCandidate(
    candidates: string[],
    password: string,
    rememberMe: boolean,
    hasTenantContext: boolean,
    index: number
  ): void {
    const username = candidates[index];

    this.authService
      .login({ username, password, rememberMe })
      .pipe(finalize(() => {
        if (index >= candidates.length - 1) {
          this.loading = false;
        }
      }))
      .subscribe({
        next: async () => {
          this.loading = false;
          this.messageService.add({
            severity: 'success',
            summary: 'Success',
            detail: 'Login successful! Redirecting...'
          });

          const landingRoute = await this.resolveLandingRoute(hasTenantContext);
          window.location.href = landingRoute;
        },
        error: (error) => {
          const isInvalidCredentials =
            (error?.error?.error_description as string | undefined)?.toLowerCase().includes('invalid username or password') ?? false;

          if (isInvalidCredentials && index < candidates.length - 1) {
            this.tryLoginCandidate(candidates, password, rememberMe, hasTenantContext, index + 1);
            return;
          }

          this.loading = false;
          console.error('Login error:', error);
          this.messageService.add({
            severity: 'error',
            summary: 'Login Failed',
            detail: error?.error?.error_description || 'Invalid username or password'
          });
        }
      });
  }

  private buildLoginCandidates(
    primaryUsername: string,
    originalUsername: string,
    hasTenantContext: boolean
  ): string[] {
    const candidates = new Set<string>();
    const primary = (primaryUsername ?? '').trim();
    const original = (originalUsername ?? '').trim();

    if (primary) {
      candidates.add(primary);
    }

    if (original) {
      candidates.add(original);
    }

    // Tenant admins are commonly seeded with username "admin" while UI collects email.
    if (hasTenantContext && (original.includes('@') || primary.includes('@'))) {
      candidates.add('admin');
    }

    return Array.from(candidates);
  }

  private async resolveLandingRoute(hasTenantContext: boolean): Promise<string> {
    try {
      const configuration = await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'GET',
            url: '/api/abp/application-configuration',
          },
          { apiName: 'Default' }
        )
      );

      const grantedPolicies = configuration?.auth?.grantedPolicies ?? {};
      const isHost = grantedPolicies['OrderXChange.Dashboard.Host'] === true;
      const isTenant = grantedPolicies['OrderXChange.Dashboard.Tenant'] === true;

      if (hasTenantContext) {
        if (isTenant) {
          return '/talabat-dashboard';
        }

        // Tenant login should never land on host-only pages.
        return '/dashboard';
      }

      if (isHost && !isTenant) {
        return '/saas/tenants';
      }

      if (isTenant) {
        return '/talabat-dashboard';
      }

      if (isHost) {
        return '/saas/tenants';
      }
    } catch (error) {
      console.warn('Failed to resolve landing route from application configuration', error);
    }

    return hasTenantContext ? '/talabat-dashboard' : '/dashboard';
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
    // Ensure stale tenant cookies don't force tenant context for host logins.
    const cookieNames = ['__tenant', 'Abp.TenantId', 'AbpTenantId'];
    const parentDomain = this.getParentDomain(location.hostname);

    for (const name of cookieNames) {
      this.clearCookie(name);
      this.clearCookie(name, location.hostname);
      if (parentDomain) {
        this.clearCookie(name, `.${parentDomain}`);
      }
    }
    this.tenantAuthValue = null;
    this.clearOAuthTenantContext();
    this.sessionState.setTenant(null);
  }

  private async ensureTenantCookieForAuthentication(tenantLookupResult: any, tenantName: string): Promise<void> {
    const tenantId = await this.resolveTenantId(tenantLookupResult, tenantName);
    const tenantValueForCookie = tenantId || (tenantName ?? '').trim() || '';
    if (!tenantValueForCookie) {
      this.tenantAuthValue = null;
      return;
    }
    this.tenantAuthValue = tenantId || tenantValueForCookie;

    const cookieValues: Record<string, string> = {
      __tenant: tenantValueForCookie,
    };
    if (tenantId) {
      cookieValues['Abp.TenantId'] = tenantId;
      cookieValues['AbpTenantId'] = tenantId;
    }

    const parentDomain = this.getParentDomain(location.hostname);

    for (const [name, value] of Object.entries(cookieValues)) {
      this.writeCookie(name, value);
      this.writeCookie(name, value, location.hostname);
      if (parentDomain) {
        this.writeCookie(name, value, `.${parentDomain}`);
      }
    }
  }

  private async resolveTenantId(tenantLookupResult: any, tenantName: string): Promise<string | undefined> {
    const immediateId = this.tryExtractTenantId(tenantLookupResult);
    if (immediateId) {
      return immediateId;
    }

    if (!tenantName) {
      return undefined;
    }

    const encodedTenant = encodeURIComponent(tenantName.trim());
    const url = `/api/abp/multi-tenancy/tenants/by-name/${encodedTenant}`;

    try {
      const result = await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'GET',
            url,
          },
          { apiName: 'Default' }
        )
      );

      return this.tryExtractTenantId(result);
    } catch {
      return undefined;
    }
  }

  private tryExtractTenantId(source: any): string | undefined {
    const candidate =
      source?.tenantId ??
      source?.id ??
      source?.tenant?.id ??
      source?.result?.tenantId ??
      source?.result?.id ??
      source?.result?.tenant?.id;

    const value = (candidate ?? '').toString().trim();
    return value || undefined;
  }

  private writeCookie(name: string, value: string, domain?: string): void {
    const encodedValue = encodeURIComponent(value);
    const secure = location.protocol === 'https:' ? '; Secure' : '';
    const domainPart = domain ? `; domain=${domain}` : '';
    document.cookie = `${name}=${encodedValue}; path=/${domainPart}; Max-Age=2592000; SameSite=Lax${secure}`;
  }

  private clearCookie(name: string, domain?: string): void {
    const secure = location.protocol === 'https:' ? '; Secure' : '';
    const domainPart = domain ? `; domain=${domain}` : '';
    document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/${domainPart}; SameSite=Lax${secure}`;
  }

  private getParentDomain(hostname: string): string | undefined {
    if (!hostname || hostname === 'localhost' || /^\d{1,3}(\.\d{1,3}){3}$/.test(hostname)) {
      return undefined;
    }

    const parts = hostname.split('.').filter(Boolean);
    if (parts.length < 2) {
      return undefined;
    }

    return parts.slice(-2).join('.');
  }

  private setOAuthTenantContext(tenantName: string): void {
    const value = (tenantName ?? '').trim();
    if (!value) {
      this.clearOAuthTenantContext();
      return;
    }

    const oauth = this.oAuthService as any;
    oauth.customQueryParams = { ...(oauth.customQueryParams ?? {}), __tenant: value };
    oauth.customTokenParameters = { ...(oauth.customTokenParameters ?? {}), __tenant: value };
  }

  private clearOAuthTenantContext(): void {
    const oauth = this.oAuthService as any;

    if (oauth.customQueryParams) {
      const { __tenant, ...rest } = oauth.customQueryParams;
      oauth.customQueryParams = rest;
    }

    if (oauth.customTokenParameters) {
      const { __tenant, ...rest } = oauth.customTokenParameters;
      oauth.customTokenParameters = rest;
    }
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
    this.router.navigate(['/forgot-password']);
  }
}
