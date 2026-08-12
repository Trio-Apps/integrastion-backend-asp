import { RoutesService, eLayoutType } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';
import { PrimeIcons } from 'primeng/api';

export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

const app = eLayoutType.application;
const TENANT = 'OrderXChange.Dashboard.Tenant';
const HOST = 'OrderXChange.Dashboard.Host';
const ANY = `${HOST} || ${TENANT}`;

function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
    // ── Home: kept as the "/" route target but hidden from the sidebar
    //    (it has no children, so as a top-level entry it rendered an empty "HOME" header).
    {
      path: '/',
      name: '::Menu:Home',
      iconClass: 'pi pi-home',
      order: 1,
      layout: app,
      invisible: true,
      requiredPolicy: ANY,
    },

    // ══════════ SaaS (host only) ══════════
    { name: '::Menu:Saas', iconClass: 'fas fa-users-cog', order: 2, layout: app, requiredPolicy: HOST },
    {
      path: '/saas/tenants', name: '::Menu:Tenants', parentName: '::Menu:Saas',
      iconClass: 'pi pi-building', order: 1, layout: app, requiredPolicy: HOST,
    },
    {
      path: '/saas/smtp-config', name: 'SMTP Config', parentName: '::Menu:Saas',
      iconClass: 'pi pi-envelope', order: 2, layout: app, requiredPolicy: HOST,
    },

    // ══════════ Dashboard (overview + sync monitoring) ══════════
    { name: '::Menu:Dashboard', iconClass: 'pi pi-chart-bar', order: 10, layout: app, requiredPolicy: ANY },
    {
      path: '/talabat-dashboard', name: 'Dashboard', parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-chart-line', order: 1, layout: app, requiredPolicy: TENANT,
    },
    {
      path: '/hangfire-monitoring', name: 'Menu Synchronization', parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-sync', order: 2, layout: app, requiredPolicy: ANY,
    },
    {
      path: '/menu-sync-diagnostics', name: 'Sync Diagnostics', parentName: '::Menu:Dashboard',
      iconClass: 'pi pi-search', order: 3, layout: app, requiredPolicy: ANY,
    },

    // ══════════ Catalog (menu content pushed to Talabat) ══════════
    { name: 'Catalog', iconClass: 'pi pi-book', order: 20, layout: app, requiredPolicy: TENANT },
    {
      path: '/categories', name: 'Categories', parentName: 'Catalog',
      iconClass: 'pi pi-list', order: 1, layout: app, requiredPolicy: TENANT,
    },
    {
      path: '/talabat-payment-methods', name: 'Payment Methods', parentName: 'Catalog',
      iconClass: 'pi pi-credit-card', order: 2, layout: app, requiredPolicy: TENANT,
    },
    {
      path: '/talabat-delivery-charges', name: 'Delivery Charges', parentName: 'Catalog',
      iconClass: 'pi pi-truck', order: 3, layout: app, requiredPolicy: TENANT,
    },
    {
      path: '/availability', name: 'Availability', parentName: 'Catalog',
      iconClass: 'pi pi-check-circle', order: 4, layout: app,
      requiredPolicy: 'OrderXChange.Availability',
    },

    // ══════════ Orders ══════════
    { name: 'Orders', iconClass: 'pi pi-shopping-cart', order: 30, layout: app, requiredPolicy: `OrderXChange.Orders || ${TENANT}` },
    {
      path: '/talabat-orders', name: 'Talabat Orders', parentName: 'Orders',
      iconClass: 'pi pi-inbox', order: 1, layout: app, requiredPolicy: 'OrderXChange.Orders',
    },
    {
      path: '/talabat-customer-mapping', name: 'Customer Mapping', parentName: 'Orders',
      iconClass: 'pi pi-users', order: 2, layout: app, requiredPolicy: TENANT,
    },
    {
      path: '/failed-orders-report', name: 'Daily Report', parentName: 'Orders',
      iconClass: 'pi pi-envelope', order: 3, layout: app, requiredPolicy: 'OrderXChange.Orders',
    },

    // ══════════ Accounts & Access (setup) ══════════
    { name: 'Accounts & Access', iconClass: PrimeIcons.COG, order: 40, layout: app, requiredPolicy: TENANT },
    {
      path: '/foodics', name: '::Menu:Foodics', parentName: 'Accounts & Access',
      iconClass: PrimeIcons.BOX, order: 1, layout: app, requiredPolicy: TENANT,
    },
    {
      path: '/talabat', name: '::Menu:Talabat', parentName: 'Accounts & Access',
      iconClass: 'pi pi-shopping-bag', order: 2, layout: app, requiredPolicy: TENANT,
    },
    {
      path: '/identity/users', name: 'Users', parentName: 'Accounts & Access',
      iconClass: 'pi pi-user', order: 3, layout: app, requiredPolicy: 'AbpIdentity.Users',
    },
    {
      path: '/identity/roles', name: 'Branch Access', parentName: 'Accounts & Access',
      iconClass: 'pi pi-sitemap', order: 4, layout: app, requiredPolicy: 'OrderXChange.Branches.Manage',
    },
  ]);
}
