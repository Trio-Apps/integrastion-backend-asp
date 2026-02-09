import { Component, inject, OnInit } from '@angular/core';
import { GdprCookieConsentComponent } from '@volo/abp.ng.gdpr/config';
import { DynamicLayoutComponent, ReplaceableComponentsService, RoutesService } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { eThemeLeptonXComponents } from '@abp/ng.theme.lepton-x';
import { AppLayout } from './layout/component/app.layout';
import { Login } from './login/login';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <p-toast position="top-right" />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent, ToastModule],
})
export class AppComponent implements OnInit {

  replaceableComponent = inject(ReplaceableComponentsService);
  routesService = inject(RoutesService);

  ngOnInit(): void {
    this.routesService.removeByParam({name : 'AbpUiNavigation::Menu:Administration'});

    this.replaceableComponent.add({
      key: eThemeLeptonXComponents.ApplicationLayout,
      component : AppLayout
    });

    this.replaceableComponent.add({
      key : eThemeLeptonXComponents.AccountLayout,
      component : Login
    });

  }
}
