import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { StyleClassModule } from 'primeng/styleclass';
import { LocalizationModule, AuthService, SessionStateService } from '@abp/ng.core';
import { AppConfigurator } from './app.configurator';
import { LayoutService } from '../service/layout.service';
import { Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SmartSearchService, SmartSearchResultDto } from '../../search/smart-search.service';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [RouterModule, CommonModule, FormsModule, StyleClassModule, AppConfigurator, LocalizationModule],
  template: ` <div class="layout-topbar">
    <div class="layout-topbar-logo-container">
      <button class="layout-menu-button layout-topbar-action" (click)="layoutService.onMenuToggle()">
        <i class="pi pi-bars"></i>
      </button>
      <a class="layout-topbar-logo" routerLink="/">
        <span class="layout-topbar-logo-text">OrderXChange</span>
      </a>
    </div>

    <div class="topbar-search" [class.open]="open()">
      <i class="pi pi-search"></i>
      <input
        type="text"
        [ngModel]="keyword()"
        (ngModelChange)="onInput($event)"
        (focus)="open.set(true)"
        (blur)="onBlur()"
        (keydown.escape)="close()"
        placeholder="Search orders, items, branches…"
      />
      <i class="pi pi-spinner spin" *ngIf="loading()"></i>

      <div class="search-panel" *ngIf="open() && keyword().trim().length >= 2">
        <ng-container *ngIf="result() as r; else searching">
          <div class="sp-empty" *ngIf="r.totalCount === 0">No matches for "{{ r.keyword }}"</div>
          <div class="sp-group" *ngIf="r.orders.length">
            <div class="sp-h">Orders</div>
            <a class="sp-row" *ngFor="let o of r.orders" (mousedown)="go('/talabat-orders/' + o.id)">
              <i class="pi pi-inbox"></i>
              <span class="sp-main">{{ o.orderCode || o.shortCode || '—' }}</span>
              <span class="sp-sub">{{ o.customerName || o.vendorCode }}</span>
            </a>
          </div>
          <div class="sp-group" *ngIf="r.items.length">
            <div class="sp-h">Menu items</div>
            <a class="sp-row" *ngFor="let it of r.items" (mousedown)="go('/availability')">
              <i class="pi pi-list"></i>
              <span class="sp-main">{{ it.name }}</span>
              <span class="sp-sub">{{ it.categoryName || (it.sku || '') }}</span>
            </a>
          </div>
          <div class="sp-group" *ngIf="r.branches.length">
            <div class="sp-h">Branches</div>
            <div class="sp-row" *ngFor="let b of r.branches">
              <i class="pi pi-sitemap"></i>
              <span class="sp-main">{{ b.branchName }}</span>
            </div>
          </div>
        </ng-container>
        <ng-template #searching><div class="sp-empty">Searching…</div></ng-template>
      </div>
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
  styles: [
    `
      .topbar-search {
        position: relative;
        display: flex;
        align-items: center;
        gap: 8px;
        flex: 1;
        max-width: 460px;
        margin: 0 1.5rem;
        background: var(--surface-ground, #f1f5f9);
        border: 1px solid transparent;
        border-radius: 10px;
        padding: 0.5rem 0.85rem;
        transition: border-color 0.15s, background 0.15s;
      }
      .topbar-search.open { background: var(--surface-card); border-color: var(--primary-color, #276d64); }
      .topbar-search > .pi-search { color: var(--primary-color, #276d64); font-size: 1rem; }
      .topbar-search input { flex: 1; border: none; outline: none; background: transparent; font-size: 0.9rem; color: var(--text-color); }
      .topbar-search .spin { animation: tb-spin 0.9s linear infinite; color: var(--text-color-secondary); }
      @keyframes tb-spin { to { transform: rotate(360deg); } }

      .search-panel {
        position: absolute;
        top: calc(100% + 8px);
        left: 0;
        right: 0;
        background: var(--surface-card);
        border: 1px solid var(--surface-border);
        border-radius: 12px;
        box-shadow: 0 12px 34px rgba(9, 40, 34, 0.16);
        max-height: 60vh;
        overflow-y: auto;
        z-index: 1100;
        padding: 6px;
      }
      .sp-empty { padding: 1rem; text-align: center; color: var(--text-color-secondary); font-size: 0.85rem; }
      .sp-h {
        font-size: 0.68rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--text-color-secondary);
        padding: 8px 10px 4px;
      }
      .sp-row {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 8px 10px;
        border-radius: 8px;
        text-decoration: none;
        color: var(--text-color);
        cursor: pointer;
      }
      .sp-row:hover { background: var(--surface-hover); }
      .sp-row > .pi { color: var(--primary-color, #276d64); font-size: 0.9rem; }
      .sp-main { font-size: 0.85rem; font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
      .sp-sub { font-size: 0.78rem; color: var(--text-color-secondary); margin-left: auto; white-space: nowrap; }
    `,
  ],
})
export class AppTopbar {
  private readonly svc = inject(SmartSearchService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly query$ = new Subject<string>();

  readonly keyword = signal('');
  readonly result = signal<SmartSearchResultDto | null>(null);
  readonly loading = signal(false);
  readonly open = signal(false);

  constructor(
    public layoutService: LayoutService,
    private authService: AuthService,
    private router: Router,
    private sessionState: SessionStateService,
  ) {
    this.query$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
        switchMap(k => {
          const term = k.trim();
          if (term.length < 2) {
            this.result.set(null);
            this.loading.set(false);
            return of(null);
          }
          this.loading.set(true);
          return this.svc.search(term);
        }),
      )
      .subscribe({
        next: r => {
          this.loading.set(false);
          if (r) this.result.set(r);
        },
        error: () => this.loading.set(false),
      });
  }

  onInput(value: string): void {
    this.keyword.set(value);
    this.query$.next(value);
  }

  onBlur(): void {
    // Delay so a result click (mousedown) is processed before the panel closes.
    setTimeout(() => this.open.set(false), 180);
  }

  go(url: string): void {
    this.router.navigate([url]);
    this.close();
  }

  close(): void {
    this.open.set(false);
    this.keyword.set('');
    this.result.set(null);
  }

  private clearTenantContext(): void {
    const cookieNames = ['__tenant', 'Abp.TenantId', 'AbpTenantId'];
    for (const name of cookieNames) {
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; domain=${location.hostname}`;
    }
    this.sessionState.setTenant(null);
  }

  logout(): void {
    this.authService
      .logout()
      .pipe(
        finalize(() => {
          this.clearTenantContext();
          window.location.href = '/account/login';
        }),
      )
      .subscribe({ next: () => {}, error: () => {} });
  }
}
