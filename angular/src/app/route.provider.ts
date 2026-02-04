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
        layout: eLayoutType.application
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
        path: '',
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
      path: '/',
      name: '::Menu:Home',
      iconClass: 'fas fa-home',
      order: 1,
      layout: eLayoutType.application,
    },
    {
      path: '/dashboard',
      name: '::Menu:Dashboard',
      iconClass: 'pi pi-chart-bar',
      order: 5,
      layout: eLayoutType.application,
    },
    {
      path: '/hangfire-monitoring',
      name: 'Menu Synchronization',
      parentName: '::Menu:Dashboard',
      iconClass: PrimeIcons.HISTORY,
      order: 3,
      layout: eLayoutType.application,
    },
    {
      path: '/menu',
      name: 'Foodics Menu',
      parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-list',
      order: 4,
      layout: eLayoutType.application,
    },
    {
      path: '/talabat-dashboard',
      name: '::Menu:TalabatDashboard',
      parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-chart-line',
      order: 2,
      layout: eLayoutType.application,
    },
  ]);
}
