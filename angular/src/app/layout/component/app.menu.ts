import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import { RoutesService, ABP, LocalizationService, PermissionService, CoreModule } from '@abp/ng.core';
import { AppMenuitem } from './app.menuitem';

@Component({
    selector: 'app-menu',
    standalone: true,
    imports: [CommonModule, AppMenuitem, RouterModule , CoreModule],
    template: `<ul class="layout-menu">
        <ng-container *ngFor="let item of model; let i = index">
            <li app-menuitem *ngIf="!item.separator" [item]="item" [index]="i" [root]="true"></li>
            <li *ngIf="item.separator" class="menu-separator"></li>
        </ng-container>
    </ul> `
})
export class AppMenu implements OnInit, OnDestroy {
    model: MenuItem[] = [];
    private destroy$ = new Subject<void>();
    
    private routesService = inject(RoutesService);
    private localizationService = inject(LocalizationService);
    private permissionService = inject(PermissionService);

    ngOnInit() {
        // Subscribe to routes changes from ABP RoutesService
        this.routesService.flat$
            .pipe(takeUntil(this.destroy$))
            .subscribe((routes) => {
                this.model = this.buildMenuFromRoutes(routes);
            });
    }

    ngOnDestroy() {
        this.destroy$.next();
        this.destroy$.complete();
    }

    /**
     * Builds PrimeNG menu structure from ABP routes
     */
    private buildMenuFromRoutes(routes: ABP.Route[]): MenuItem[] {
        // Filter visible routes and sort by order
        const visibleRoutes = routes
            .filter(route => route.invisible !== true && this.hasPermission(route))
            .sort((a, b) => (a.order || 0) - (b.order || 0));

        // Group routes by parent
        const topLevelRoutes = visibleRoutes.filter(route => !route.parentName);
        
        return topLevelRoutes.map(route => this.mapRouteToMenuItem(route, visibleRoutes));
    }

    /**
     * Maps ABP Route to PrimeNG MenuItem
     */
    private mapRouteToMenuItem(route: ABP.Route, allRoutes: ABP.Route[]): MenuItem {
        const children = allRoutes.filter(r => r.parentName === route.name);
        
        const menuItem: MenuItem = {
            label: this.getRouteLabel(route),
            icon: this.convertIconClass(route.iconClass),
            routerLink: route.path ? [route.path] : undefined,
            visible: route.invisible !== true
        };

        // Add children if they exist
        if (children.length > 0) {
            menuItem.items = children.map(child => this.mapRouteToMenuItem(child, allRoutes));
        }

        return menuItem;
    }

    /**
     * Gets the localized label for a route using ABP LocalizationService
     */
    private getRouteLabel(route: ABP.Route): string {
        if (!route.name) {
            return '';
        }
        
        // ABP uses localization keys starting with '::'
        if (route.name.startsWith('::')) {
            // Use LocalizationService to get the translated text
            return this.localizationService.instant(route.name);
        }
        
        return route.name;
    }

    /**
     * Converts Font Awesome classes to PrimeNG icon classes
     */
    private convertIconClass(iconClass?: string): string {
        if (!iconClass) {
            return 'pi pi-fw pi-circle';
        }

        // If it's already a PrimeNG icon, return as is
        if (iconClass.includes('pi-')) {
            return iconClass;
        }

        // Map common Font Awesome icons to PrimeNG icons
        const iconMap: { [key: string]: string } = {
            'fa-home': 'pi-home',
            'fa-chart-line': 'pi-chart-line',
            'fa-users': 'pi-users',
            'fa-cog': 'pi-cog',
            'fa-file': 'pi-file',
            'fa-folder': 'pi-folder',
            'fa-dashboard': 'pi-chart-bar',
            'fa-table': 'pi-table',
            'fa-user': 'pi-user',
            'fa-lock': 'pi-lock',
            'fa-envelope': 'pi-envelope',
            'fa-bell': 'pi-bell',
            'fa-calendar': 'pi-calendar',
            'fa-search': 'pi-search'
        };

        // Extract the icon name from Font Awesome class
        const iconMatch = iconClass.match(/fa-[\w-]+/);
        if (iconMatch) {
            const faIcon = iconMatch[0];
            const piIcon = iconMap[faIcon];
            return piIcon ? `pi pi-fw ${piIcon}` : 'pi pi-fw pi-circle';
        }

        return 'pi pi-fw pi-circle';
    }

    /**
     * Checks if user has permission to view the route
     */
    private hasPermission(route: ABP.Route): boolean {
        if (!route.requiredPolicy) {
            return true;
        }

        // Handle OR conditions in policy (e.g., "Policy1 || Policy2")
        const policies = route.requiredPolicy.split('||').map(p => p.trim());
        
        return policies.some(policy => 
            this.permissionService.getGrantedPolicy(policy)
        );
    }
}
