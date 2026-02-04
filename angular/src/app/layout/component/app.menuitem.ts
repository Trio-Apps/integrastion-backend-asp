import { Component, HostBinding, Input } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { RippleModule } from 'primeng/ripple';
import { MenuItem } from 'primeng/api';
import { LayoutService } from '../service/layout.service';
import { CoreModule } from '@abp/ng.core';

@Component({
    // eslint-disable-next-line @angular-eslint/component-selector
    selector: '[app-menuitem]',
    imports: [CommonModule, RouterModule, RippleModule, CoreModule],
    template: `
        <ng-container>
            @if (root && item.visible !== false) {
                <div class="layout-menuitem-root-text">{{ item.label | abpLocalization}}</div>
            }
            
            <!-- Non-routable items (parent items or external links) -->
            @if ((!item.routerLink || item.items) && item.visible !== false) {
                <a *abpPermission="getRequiredPolicy()"
                   [attr.href]="item.url" 
                   (click)="itemClick($event)" 
                   [ngClass]="item.styleClass" 
                   [attr.target]="item.target" 
                   tabindex="0" 
                   pRipple>
                    <!-- Material Icon Support -->
                    @if (isMaterialIcon(item.icon)) {
                        <span [ngClass]="getMaterialIconClass(item.icon)" class="layout-menuitem-icon">
                            {{ getMaterialIconName(item.icon) }}
                        </span>
                    } @else {
                        <!-- PrimeNG Icon Support -->
                        <i [ngClass]="getPrimeIconClass(item.icon)" class="layout-menuitem-icon"></i>
                    }
                    
                    <span class="layout-menuitem-text">{{ item.label | abpLocalization }}</span>
                    @if (item.items) {
                        <i class="pi pi-fw pi-angle-down layout-submenu-toggler"></i>
                    }
                </a>
            }
            
            <!-- Routable items (leaf items) -->
            @if (item.routerLink && !item.items && item.visible !== false) {
                <a *abpPermission="getRequiredPolicy()"
                    (click)="itemClick($event)"
                    [ngClass]="item.styleClass"
                    [routerLink]="item.routerLink"
                    routerLinkActive="active-route"
                    [routerLinkActiveOptions]="item.routerLinkActiveOptions || { paths: 'exact', queryParams: 'ignored', matrixParams: 'ignored', fragment: 'ignored' }"
                    [fragment]="item.fragment"
                    [queryParamsHandling]="item.queryParamsHandling"
                    [preserveFragment]="item.preserveFragment"
                    [skipLocationChange]="item.skipLocationChange"
                    [replaceUrl]="item.replaceUrl"
                    [state]="item.state"
                    [queryParams]="item.queryParams"
                    [attr.target]="item.target"
                    tabindex="0"
                    pRipple>
                    <!-- Material Icon Support -->
                    @if (isMaterialIcon(item.icon)) {
                        <span [ngClass]="getMaterialIconClass(item.icon)" class="layout-menuitem-icon">
                            {{ getMaterialIconName(item.icon) }}
                        </span>
                    } @else {
                        <!-- PrimeNG Icon Support -->
                        <i [ngClass]="getPrimeIconClass(item.icon)" class="layout-menuitem-icon"></i>
                    }
                    
                    <span class="layout-menuitem-text">{{ item.label | abpLocalization }}</span>
                    @if (item.items) {
                        <i class="pi pi-fw pi-angle-down layout-submenu-toggler"></i>
                    }
                </a>
            }

            <!-- Submenu items with animation -->
            @if (item.items && item.visible !== false) {
                <ul [@children]="submenuAnimation">
                    @for (child of item.items; track child; let i = $index) {
                        <li app-menuitem [item]="child" [index]="i" [parentKey]="key" [class]="child['badgeClass']"></li>
                    }
                </ul>
            }
        </ng-container>
    `,
    animations: [
        trigger('children', [
            state(
                'collapsed',
                style({
                    height: '0'
                })
            ),
            state(
                'expanded',
                style({
                    height: '*'
                })
            ),
            transition('collapsed <=> expanded', animate('400ms cubic-bezier(0.86, 0, 0.07, 1)'))
        ])
    ],
    providers: [LayoutService]
})
/**
 * Enhanced AppMenuitem Component
 * 
 * Features:
 * - Supports both PrimeNG icons and Material icons
 * - Uses ABP *abpPermission directive (supports ||, && operators)
 * - Auto-converts Font Awesome icons to PrimeNG equivalents
 * - Maintains existing animation and routing behavior
 * - Fully compatible with ABP RoutesService
 * 
 * Based on PrimeNG Sakai template with ABP integration
 * @see https://sakai.primeng.org
 * @see https://abp.io/docs/latest/framework/ui/angular/permission-management#permission-directive
 */
export class AppMenuitem {
    @Input() item!: MenuItem;

    @Input() index!: number;

    @Input() @HostBinding('class.layout-root-menuitem') root!: boolean;

    @Input() parentKey!: string;

    active = false;

    menuSourceSubscription: Subscription;

    menuResetSubscription: Subscription;

    key: string = '';

    constructor(
        public router: Router,
        private layoutService: LayoutService
    ) {
        this.menuSourceSubscription = this.layoutService.menuSource$.subscribe((value) => {
            Promise.resolve(null).then(() => {
                if (value.routeEvent) {
                    this.active = value.key === this.key || value.key.startsWith(this.key + '-') ? true : false;
                } else {
                    if (value.key !== this.key && !value.key.startsWith(this.key + '-')) {
                        this.active = false;
                    }
                }
            });
        });

        this.menuResetSubscription = this.layoutService.resetSource$.subscribe(() => {
            this.active = false;
        });

        this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe((params) => {
            if (this.item.routerLink) {
                this.updateActiveStateFromRoute();
            }
        });
    }

    ngOnInit() {
        this.key = this.parentKey ? this.parentKey + '-' + this.index : String(this.index);

        if (this.item.routerLink) {
            this.updateActiveStateFromRoute();
        }
    }

    updateActiveStateFromRoute() {
        let activeRoute = this.router.isActive(this.item.routerLink[0], { paths: 'exact', queryParams: 'ignored', matrixParams: 'ignored', fragment: 'ignored' });

        if (activeRoute) {
            this.layoutService.onMenuStateChange({ key: this.key, routeEvent: true });
        }
    }

    itemClick(event: Event) {
        // avoid processing disabled items
        if (this.item.disabled) {
            event.preventDefault();
            return;
        }

        // execute command
        if (this.item.command) {
            this.item.command({ originalEvent: event, item: this.item });
        }

        // toggle active state
        if (this.item.items) {
            this.active = !this.active;
        }

        this.layoutService.onMenuStateChange({ key: this.key });
    }

    get submenuAnimation() {
        return this.root ? 'expanded' : this.active ? 'expanded' : 'collapsed';
    }

    @HostBinding('class.active-menuitem')
    get activeClass() {
        return this.active && !this.root;
    }

    /**
     * Checks if the icon class represents a Material icon
     * Material icons typically use 'material-icons' or 'material-symbols' class
     */
    isMaterialIcon(iconClass?: string): boolean {
        if (!iconClass) {
            return false;
        }
        return iconClass.includes('material-icons') || iconClass.includes('material-symbols');
    }

    /**
     * Extracts and returns the Material icon class
     * Example: 'material-icons outlined' or 'material-symbols-outlined'
     */
    getMaterialIconClass(iconClass?: string): string {
        if (!iconClass) {
            return 'material-icons';
        }
        
        // Extract the base class and variant
        const parts = iconClass.split(' ');
        const baseClass = parts.find(p => p.includes('material')) || 'material-icons';
        const variant = parts.find(p => !p.includes('material') && !this.isMaterialIconName(p));
        
        return variant ? `${baseClass} ${variant}` : baseClass;
    }

    /**
     * Extracts and returns the Material icon name
     * Example: from 'material-icons home' returns 'home'
     */
    getMaterialIconName(iconClass?: string): string {
        if (!iconClass) {
            return '';
        }
        
        // Split the class and find the icon name (not containing 'material')
        const parts = iconClass.split(' ');
        const iconName = parts.find(p => !p.includes('material') && p.length > 0);
        
        return iconName || '';
    }

    /**
     * Helper to identify if a string is a Material icon name
     */
    private isMaterialIconName(str: string): boolean {
        // Common Material icon names for detection
        const commonIcons = ['home', 'dashboard', 'menu', 'settings', 'person', 'search', 'close'];
        return commonIcons.includes(str.toLowerCase());
    }

    /**
     * Returns the PrimeNG icon class, ensuring proper formatting
     * Handles Font Awesome to PrimeNG icon conversion
     */
    getPrimeIconClass(iconClass?: string): string {
        if (!iconClass) {
            return 'pi pi-circle';
        }

        // If it's already a PrimeNG icon, return as is
        if (iconClass.includes('pi-')) {
            return iconClass.includes('pi ') ? iconClass : `pi ${iconClass}`;
        }

        // Map Font Awesome icons to PrimeNG icons
        const iconMap: { [key: string]: string } = {
            'fa-home': 'pi-home',
            'fa-chart-line': 'pi-chart-line',
            'fa-users': 'pi-users',
            'fa-user': 'pi-user',
            'fa-cog': 'pi-cog',
            'fa-file': 'pi-file',
            'fa-folder': 'pi-folder',
            'fa-dashboard': 'pi-chart-bar',
            'fa-table': 'pi-table',
            'fa-lock': 'pi-lock',
            'fa-envelope': 'pi-envelope',
            'fa-bell': 'pi-bell',
            'fa-calendar': 'pi-calendar',
            'fa-search': 'pi-search',
            'fa-plus': 'pi-plus',
            'fa-minus': 'pi-minus',
            'fa-edit': 'pi-pencil',
            'fa-trash': 'pi-trash',
            'fa-save': 'pi-save',
            'fa-download': 'pi-download',
            'fa-upload': 'pi-upload',
            'fa-check': 'pi-check',
            'fa-times': 'pi-times',
            'fa-arrow-left': 'pi-arrow-left',
            'fa-arrow-right': 'pi-arrow-right'
        };

        // Extract Font Awesome icon name
        const faIconMatch = iconClass.match(/fa-[\w-]+/);
        if (faIconMatch) {
            const faIcon = faIconMatch[0];
            const piIcon = iconMap[faIcon];
            return piIcon ? `pi pi-fw ${piIcon}` : 'pi pi-fw pi-circle';
        }

        return iconClass;
    }

    /**
     * Gets the required policy for the menu item
     * Returns the policy string to be used by the *abpPermission directive
     * 
     * The directive supports:
     * - Single policy: 'Policy1'
     * - OR conditions: 'Policy1 || Policy2'
     * - AND conditions: 'Policy1 && Policy2'
     * - Empty string '' returns true (no permission required)
     * 
     * @see https://abp.io/docs/latest/framework/ui/angular/permission-management#permission-directive
     */
    getRequiredPolicy(): string {
        // Get the requiredPolicy from the item (custom property from ABP routes)
        const requiredPolicy = (this.item as any).requiredPolicy;
        
        // If no policy is specified, return empty string (allows access)
        return requiredPolicy || '';
    }

    ngOnDestroy() {
        if (this.menuSourceSubscription) {
            this.menuSourceSubscription.unsubscribe();
        }

        if (this.menuResetSubscription) {
            this.menuResetSubscription.unsubscribe();
        }
    }
}
