// custom-error-handler.service.ts
import { inject, Injectable } from "@angular/core";
import { HttpErrorResponse } from "@angular/common/http";
import { CustomHttpErrorHandlerService } from "@abp/ng.theme.shared";
import { CUSTOM_HTTP_ERROR_HANDLER_PRIORITY } from "@abp/ng.theme.shared";
import { MessageService } from "primeng/api";
import { LocalizationService } from "@abp/ng.core";

@Injectable({ providedIn: "root"  })
export class MyCustomErrorHandlerService
    implements CustomHttpErrorHandlerService {
    readonly priority = CUSTOM_HTTP_ERROR_HANDLER_PRIORITY.veryHigh;
    protected readonly toaster = inject(MessageService);
    localize = inject(LocalizationService);
    private error: HttpErrorResponse | undefined = undefined;

    // What kind of error should be handled by this service? You can decide it in this method. If error is suitable to your case then return true; otherwise return false.
    canHandle(error: unknown): boolean {
        if (error instanceof HttpErrorResponse && (error.status === 400 || error.status === 403 || error.status === 500 ||  error.status === 0)) {
            this.error = error;
            return true;
        }
        return false;
    }

    execute() {
        if (!this.error) {
            return;
        }

        if (this.error.status === 400) {
            this.toaster.add({
                severity: 'error', 
                summary: this.localize.instant('::Error'),
                detail: this.error.error?.error?.details || "Bad Request!",
                life: 3000,
            });
        } else {
            this.toaster.add({
                severity: 'error', 
                summary: this.localize.instant('::Error'),
                detail: this.error.error?.error?.message || "An error occurred!",
                life: 3000,
            });
        }
    }
}
