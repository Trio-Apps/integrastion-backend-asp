import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { StyleClassModule } from 'primeng/styleclass';
import { LocalizationModule, AuthService, SessionStateService } from '@abp/ng.core';
import { AppConfigurator } from './app.configurator';
import { LayoutService } from '../service/layout.service';
import { finalize } from 'rxjs/operators';


@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [RouterModule, CommonModule, StyleClassModule, AppConfigurator, LocalizationModule],
  template: ` <div class="layout-topbar">
    <div class="layout-topbar-logo-container">
      <button class="layout-menu-button layout-topbar-action" (click)="layoutService.onMenuToggle()">
        <i class="pi pi-bars"></i>
      </button>
      <a class="layout-topbar-logo" routerLink="/">
        <img
          src="assets/images/logo/newlogo.png"
          alt="BEON-IT"
          class="layout-topbar-logo-image"
        />
      </a>
    </div>
    <div class="layout-topbar-menu ms-auto">
      <button
        type="button"
        class="layout-topbar-action"
        (click)="logout()"
        [attr.title]="'::Common.Logout' | abpLocalization"
      >
        <i class="pi pi-sign-out"></i>
      </button>
    </div>
  </div>`,
})
export class AppTopbar {
  constructor(
    public layoutService: LayoutService,
    private authService: AuthService,
    private router: Router,
    private sessionState: SessionStateService,

  ) {}

  private clearTenantContext(): void {
    // ABP default tenant cookie key is "__tenant" (can be customized via TENANT_KEY).
    // If this cookie stays around, the next login can still be resolved as Host/old-tenant.
    const cookieNames = ['__tenant', 'Abp.TenantId', 'AbpTenantId'];

    for (const name of cookieNames) {
      // Clear for root path
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
      // Clear for current domain (helps when cookie was set with explicit domain)
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; domain=${location.hostname}`;
    }

    // Clear ABP tenant state (in-memory / storage).
    this.sessionState.setTenant(null);
  }

 
  logout(): void {
    // ABP AuthService.logout() returns an Observable and performs the proper logout flow.
    // We subscribe to ensure it executes, then navigate to login.
    this.authService
      .logout()
      .pipe(
        finalize(() => {
          this.clearTenantContext();

          // Hard redirect ensures app re-initializes without stale tenant context.
          window.location.href = '/account/login';
        })
      )
      .subscribe({
        next: () => {},
        error: () => {},
      });
  }
}
