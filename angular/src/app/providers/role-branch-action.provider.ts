import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DialogService } from 'primeng/dynamicdialog';
import { EntityAction } from '@abp/ng.components/extensible';
import {
  IDENTITY_ENTITY_ACTION_CONTRIBUTORS,
  IDENTITY_CREATE_FORM_PROP_CONTRIBUTORS,
  IDENTITY_EDIT_FORM_PROP_CONTRIBUTORS,
} from '@abp/ng.identity';
import { IdentityRoleDto, IdentityUserDto } from '@abp/ng.identity/proxy';
import { RoleBranchModalComponent } from '../identity/role-branch-modal/role-branch-modal.component';

// Roles grid: "Branches" action → branch-scoped access dialog.
function branchesActionContributor(actionList: any) {
  const actions = EntityAction.createMany<IdentityRoleDto>([
    {
      text: 'Branches',
      action: data => {
        const dialogService = data.getInjected(DialogService);
        const role = data.record;
        dialogService.open(RoleBranchModalComponent, {
          header: `Branch Access — ${role?.name ?? 'Role'}`,
          width: '480px',
          data: {
            roleId: role?.id,
            roleName: role?.name,
          },
          modal: true,
          closable: true,
        });
      },
      permission: 'OrderXChange.Branches.Manage',
    },
  ]);

  actionList.addMany(actions);
}

// Users grid: "Reset login attempts" action → clears failed attempts and unlocks the account.
function resetLoginAttemptsContributor(actionList: any) {
  const actions = EntityAction.createMany<IdentityUserDto>([
    {
      text: 'Reset login attempts',
      action: data => {
        const rest = data.getInjected(RestService);
        const toaster = data.getInjected(ToasterService);
        const user = data.record;
        rest
          .request<null, void>(
            { method: 'POST', url: `/api/app/user-lockout/${user.id}/reset` },
            { apiName: 'Default' },
          )
          .subscribe({
            next: () => toaster.success('Login attempts reset; account unlocked.', 'Success'),
            error: () => toaster.error('Could not reset login attempts.', 'Error'),
          });
      },
      permission: 'AbpIdentity.Users.Update',
    },
  ]);

  actionList.addMany(actions);
}

// Users form: hide the confusing "Account lockout" (lockoutEnabled) field.
function hideLockoutFormProp(propList: any) {
  propList.dropByValue('lockoutEnabled', (prop: any) => prop.name);
}

export const IDENTITY_UI_PROVIDERS = [
  {
    provide: IDENTITY_ENTITY_ACTION_CONTRIBUTORS,
    useValue: {
      'Identity.RolesComponent': [branchesActionContributor],
      'Identity.UsersComponent': [resetLoginAttemptsContributor],
    },
  },
  {
    provide: IDENTITY_CREATE_FORM_PROP_CONTRIBUTORS,
    useValue: {
      'Identity.UsersComponent': [hideLockoutFormProp],
    },
  },
  {
    provide: IDENTITY_EDIT_FORM_PROP_CONTRIBUTORS,
    useValue: {
      'Identity.UsersComponent': [hideLockoutFormProp],
    },
  },
];
