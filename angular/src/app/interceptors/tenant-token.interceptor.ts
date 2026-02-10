import { HttpEvent, HttpHandler, HttpInterceptor, HttpParams, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { SessionStateService } from '@abp/ng.core';
import { Observable } from 'rxjs';

@Injectable()
export class TenantTokenInterceptor implements HttpInterceptor {
  private readonly sessionState = inject(SessionStateService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!this.isTokenRequest(req.url)) {
      return next.handle(req);
    }

    const tenant = this.resolveTenantForAuth();
    if (!tenant) {
      return next.handle(req);
    }

    let headers = req.headers;
    if (!headers.has('__tenant')) {
      headers = headers.set('__tenant', tenant);
    }

    let url = req.url;
    if (!this.urlContainsTenant(url)) {
      const delimiter = url.includes('?') ? '&' : '?';
      url = `${url}${delimiter}__tenant=${encodeURIComponent(tenant)}`;
    }

    let body = req.body;

    if (typeof body === 'string') {
      if (!this.formEncodedContainsTenant(body)) {
        body = `${body}&__tenant=${encodeURIComponent(tenant)}`;
      }
    } else if (body instanceof HttpParams) {
      if (!body.has('__tenant')) {
        body = body.set('__tenant', tenant);
      }
    } else if (body && typeof body === 'object' && !('__tenant' in (body as Record<string, unknown>))) {
      body = { ...(body as Record<string, unknown>), __tenant: tenant };
    }

    return next.handle(req.clone({ headers, body, url }));
  }

  private isTokenRequest(url: string): boolean {
    return /\/connect\/token(\?|$)/i.test(url);
  }

  private formEncodedContainsTenant(body: string): boolean {
    return /(^|&)__tenant=/.test(body);
  }

  private urlContainsTenant(url: string): boolean {
    return /[?&]__tenant=/.test(url);
  }

  private resolveTenantForAuth(): string | null {
    const currentTenant = this.sessionState.getTenant?.();
    const fromSession =
      (currentTenant?.id ?? currentTenant?.name ?? '').toString().trim();

    if (fromSession) {
      return fromSession;
    }

    const fromCookie =
      this.getCookie('__tenant') ||
      this.getCookie('Abp.TenantId') ||
      this.getCookie('AbpTenantId');

    return fromCookie?.trim() || null;
  }

  private getCookie(name: string): string | null {
    const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const match = document.cookie.match(new RegExp(`(?:^|; )${escaped}=([^;]*)`));
    return match ? decodeURIComponent(match[1]) : null;
  }
}
