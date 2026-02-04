import { provideAbpCore, withOptions } from '@abp/ng.core';
import { provideAbpOAuth } from '@abp/ng.oauth';
import { provideSettingManagementConfig } from '@abp/ng.setting-management/config';
import { provideFeatureManagementConfig } from '@abp/ng.feature-management';
import { CUSTOM_ERROR_HANDLERS, provideAbpThemeShared,withValidationBluePrint,} from '@abp/ng.theme.shared';
import { provideIdentityConfig } from '@volo/abp.ng.identity/config';
import { provideCommercialUiConfig } from '@volo/abp.commercial.ng.ui/config';
import { provideAccountAdminConfig } from '@volo/abp.ng.account/admin/config';
import { provideAccountPublicConfig } from '@volo/abp.ng.account/public/config';
import { provideGdprConfig, withCookieConsentOptions } from '@volo/abp.ng.gdpr/config';
import { provideAuditLoggingConfig } from '@volo/abp.ng.audit-logging/config';
import { provideLanguageManagementConfig } from '@volo/abp.ng.language-management/config';
import { registerLocale } from '@volo/abp.ng.language-management/locale';
import { provideTextTemplateManagementConfig } from '@volo/abp.ng.text-template-management/config';
import { provideOpeniddictproConfig } from '@volo/abp.ng.openiddictpro/config';
import { provideThemeLeptonX } from '@abp/ng.theme.lepton-x';
import { provideSideMenuLayout } from '@abp/ng.theme.lepton-x/layouts';
import { provideLogo, withEnvironmentOptions } from "@volo/ngx-lepton-x.core";
import { ApplicationConfig } from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { environment } from '../environments/environment';
import { APP_ROUTES } from './app.routes';
import { APP_ROUTE_PROVIDER } from './route.provider';
import { USER_MENU_PROVIDER } from './providers/user-menu.provider';
import { providePrimeNG } from 'primeng/config';
import { AppConfigurator } from './layout/component/app.configurator';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { MyCustomErrorHandlerService } from './MyCustomErrorHandlerService.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(APP_ROUTES),
    APP_ROUTE_PROVIDER,
    USER_MENU_PROVIDER,
    provideAnimations(),
    provideAbpCore(
      withOptions({
        environment,
        registerLocaleFn: registerLocale(),
      }),
    ),
    provideAbpOAuth(),
    provideIdentityConfig(),
    provideSettingManagementConfig(),
    provideFeatureManagementConfig(),
    provideAccountAdminConfig(),
    provideAccountPublicConfig(),
    provideCommercialUiConfig(),
    provideAbpThemeShared(
      withValidationBluePrint({
        wrongPassword: 'Please choose 1q2w3E*',
      })
    ),
    provideSideMenuLayout(),
    provideThemeLeptonX(),
    provideLogo(withEnvironmentOptions(environment)),
    provideGdprConfig(
      withCookieConsentOptions({
        cookiePolicyUrl: '/gdpr-cookie-consent/cookie',
        privacyPolicyUrl: '/gdpr-cookie-consent/privacy',
      }),
    ),
    providePrimeNG({ theme: { preset: Aura, options: { darkModeSelector: '.app-dark' } } }),
    MessageService, // PrimeNG MessageService for toast notifications
    provideLanguageManagementConfig(),
    provideAuditLoggingConfig(),
    provideOpeniddictproConfig(),
    provideTextTemplateManagementConfig(),
    {
      provide : CUSTOM_ERROR_HANDLERS,
      useExisting : MyCustomErrorHandlerService,
      multi : true,
    },
  ]
};
