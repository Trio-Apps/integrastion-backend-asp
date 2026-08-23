// custom-error-handler.service.ts
import { inject, Injectable } from "@angular/core";
import { HttpErrorResponse } from "@angular/common/http";
import { CustomHttpErrorHandlerService } from "@abp/ng.theme.shared";
import { CUSTOM_HTTP_ERROR_HANDLER_PRIORITY } from "@abp/ng.theme.shared";
import { MessageService } from "primeng/api";
import { LocalizationService } from "@abp/ng.core";

const HANDLED_STATUSES = [0, 400, 403, 404, 500];

@Injectable({ providedIn: "root"  })
export class MyCustomErrorHandlerService
    implements CustomHttpErrorHandlerService {
    readonly priority = CUSTOM_HTTP_ERROR_HANDLER_PRIORITY.veryHigh;
    protected readonly toaster = inject(MessageService);
    localize = inject(LocalizationService);
    private error: HttpErrorResponse | { status: number } | undefined = undefined;

    // What kind of error should be handled by this service? You can decide it in this method. If error is suitable to your case then return true; otherwise return false.
    // NB: ABP's permissionGuard reports a denied route as a *plain object* ({ status: 403 }), not an
    // HttpErrorResponse. Matching only on HttpErrorResponse let those fall through to ABP's default
    // handler, which navigated the browser to {apiUrl}/Error?httpStatusCode=404 — right off the app.
    canHandle(error: unknown): boolean {
        const status = (error as { status?: unknown } | null)?.status;
        if (typeof status === 'number' && HANDLED_STATUSES.includes(status)) {
            this.error = error as HttpErrorResponse | { status: number };
            return true;
        }
        return false;
    }

    execute() {
        if (!this.error) {
            return;
        }

        const status = this.error.status;
        const body = (this.error as HttpErrorResponse).error;

        if (status === 403) {
            this.toaster.add({
                severity: 'warn',
                summary: 'No access',
                detail: body?.error?.message
                    || "You don't have permission for this page. Ask an administrator to grant it to your role.",
                life: 5000,
            });
            return;
        }

        if (status === 400) {
            this.toaster.add({
                severity: 'error',
                summary: this.localize.instant('::Error'),
                detail: body?.error?.details || "Bad Request!",
                life: 3000,
            });
            return;
        }

        this.toaster.add({
            severity: 'error',
            summary: this.localize.instant('::Error'),
            detail: body?.error?.message || "An error occurred!",
            life: 3000,
        });
    }
}
