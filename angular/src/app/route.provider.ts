import { RoutesService, eLayoutType } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';
import { PrimeIcons } from 'primeng/api';

export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
      {
        path: '/',
        name: '::Menu:Home',
        iconClass: 'fas fa-home',
        order: 1,
        layout: eLayoutType.application,
        requiredPolicy: 'OrderXChange.Dashboard.Host || OrderXChange.Dashboard.Tenant'
      },

      {
        name: '::Menu:Saas',
        iconClass: 'fas fa-users-cog',
        order: 2,
        layout: eLayoutType.application,
        requiredPolicy : 'OrderXChange.Dashboard.Host'
      },

      {
        path: '/saas/tenants',
        name: '::Menu:Tenants',
        iconClass: 'fas fa-building',
        order: 1,
        parentName : '::Menu:Saas',
        layout: eLayoutType.application,
        requiredPolicy : 'OrderXChange.Dashboard.Host'
      },
      {
        path: '/saas/smtp-config',
        name: 'SMTP Config',
        iconClass: 'pi pi-envelope',
        order: 2,
        parentName : '::Menu:Saas',
        layout: eLayoutType.application,
        requiredPolicy : 'OrderXChange.Dashboard.Host'
      },
      {
        name: '::Menu:Account',
        iconClass : PrimeIcons.BOX,
        layout: eLayoutType.application,
        requiredPolicy : 'OrderXChange.Dashboard.Tenant'
      },
      
        {
          path: '/foodics',
          name: '::Menu:Foodics',
          parentName : '::Menu:Account',
          iconClass : PrimeIcons.BOX,
          layout: eLayoutType.application,
          requiredPolicy : 'OrderXChange.Dashboard.Tenant'
        },
        {
          path: '/talabat',
          name: '::Menu:Talabat',
          parentName : '::Menu:Account',
          iconClass : 'pi pi-shopping-bag',
          layout: eLayoutType.application,
          requiredPolicy : 'OrderXChange.Dashboard.Tenant'
        },
    {
      path: '/dashboard',
      name: '::Menu:Dashboard',
      iconClass: 'pi pi-chart-bar',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'OrderXChange.Dashboard.Host || OrderXChange.Dashboard.Tenant',
    },
    {
      path: '/hangfire-monitoring',
      name: 'Menu Synchronization',
      parentName: '::Menu:Dashboard',
      iconClass: PrimeIcons.HISTORY,
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'OrderXChange.Dashboard.Host || OrderXChange.Dashboard.Tenant',
    },
    {
      path: '/talabat-orders',
      name: 'Talabat Orders',
      parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-inbox',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
    {
      path: '/talabat-dashboard',
      name: '::Menu:TalabatDashboard',
      parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-chart-line',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
    {
      path: '/categories',
      name: 'Categories',
      parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-list',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'OrderXChange.Dashboard.Tenant',
    },
  ]);
}
