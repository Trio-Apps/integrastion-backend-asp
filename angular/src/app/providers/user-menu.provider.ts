import { inject } from '@angular/core';
import { provideAppInitializer } from '@angular/core';
import { map } from 'rxjs';
import { ConfigStateService, EnvironmentService } from '@abp/ng.core';
import { UserMenuService } from '@abp/ng.theme.shared';
import {
  NAVIGATE_TO_MY_EXTERNAL_LOGINS,
  NAVIGATE_TO_MY_SESSIONS,
  NAVIGATE_TO_MY_SECURITY_LOGS,
  OPEN_AUTHORITY_DELEGATION_MODAL,
  OPEN_MY_LINK_USERS_MODAL,
} from '@volo/abp.commercial.ng.ui/config';

export const USER_MENU_PROVIDER = [
  provideAppInitializer(() => {
    configureUserMenu();
  }),
];

function configureUserMenu() {
  const userMenu = inject(UserMenuService);
  const configState = inject(ConfigStateService);
  const environment = inject(EnvironmentService);

  const navigateToMySessions = inject(NAVIGATE_TO_MY_SESSIONS);
  const navigateToMyExternalLogins = inject(NAVIGATE_TO_MY_EXTERNAL_LOGINS);
  const navigateToMySecurityLogs = inject(NAVIGATE_TO_MY_SECURITY_LOGS);

  const openMyLinkUsersModal = inject(OPEN_MY_LINK_USERS_MODAL, {
    optional: true,
  }) as () => void;

  const openAuthorityDelegationModal = inject(OPEN_AUTHORITY_DELEGATION_MODAL, {
    optional: true,
  }) as () => void;

  userMenu.addItems([
    {
      id: userMenuItems.Sessions,
      order: 100,
      textTemplate: {
        icon: 'bi bi-clock-fill',
        text: 'AbpAccount::Sessions',
      },
      action: () => navigateToMySessions(),
    },
    {
      id: userMenuItems.ExternalLogins,
      order: 101,
      textTemplate: {
        icon: 'bi bi-person-circle',
        text: 'AbpAccount::ExternalLogins',
      },
      action: () => navigateToMyExternalLogins(),
      visible: () => {
        return environment.getEnvironment$().pipe(
          map(({ oAuthConfig }) => {
            return oAuthConfig?.responseType === 'code';
          })
        );
      },
    },
    {
      id: userMenuItems.LinkedAccounts,
      order: 102,
      textTemplate: {
        icon: 'bi bi-link',
        text: 'AbpAccount::LinkedAccounts',
      },
      action: () => openMyLinkUsersModal(),
      visible: () => !!openMyLinkUsersModal,
    },
    {
      id: userMenuItems.AuthorityDelegation,
      order: 103,
      textTemplate: {
        text: 'AbpAccount::AuthorityDelegation',
        icon: 'fa fa-users',
      },
      visible: () => {
        return configState
          .getOne$('currentUser')
          .pipe(map(({ impersonatorUserId }) => !Boolean(impersonatorUserId)));
      },
      action: () => openAuthorityDelegationModal(),
    },
    {
      id: userMenuItems.SecurityLogs,
      order: 105,
      textTemplate: {
        icon: 'bi bi-list-ul',
        text: 'AbpAccount::MySecurityLogs',
      },
      action: () => navigateToMySecurityLogs(),
    },
  ]);
}

enum userMenuItems {
  Sessions = 'Sessions',
  ExternalLogins = 'ExternalLogins',
  LinkedAccounts = 'LinkedAccounts',
  SecurityLogs = 'SecurityLogs',
  BackToImpersonator = 'BackToImpersonator',
  AuthorityDelegation = 'AuthorityDelegation',
}
