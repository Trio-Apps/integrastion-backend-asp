import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DialogService } from 'primeng/dynamicdialog';
import { EntityAction } from '@abp/ng.components/extensible';
import { IdentityUserDto } from '@abp/ng.identity/proxy';
import { UserBranchModalComponent } from '../identity/user-branch-modal/user-branch-modal.component';

// Users grid: "Branches" action → branch-scoped access dialog. Branches follow the person,
// not their role, so two users sharing a role can still cover different branches.
function branchesActionContributor(actionList: any) {
  const actions = EntityAction.createMany<IdentityUserDto>([
    {
      text: 'Branches',
      action: data => {
        const dialogService = data.getInjected(DialogService);
        const user = data.record;
        dialogService.open(UserBranchModalComponent, {
          header: `Branch Access — ${user?.userName ?? 'User'}`,
          width: '540px',
          data: {
            userId: user?.id,
            userName: user?.userName,
          },
          modal: true,
          closable: true,
        });
      },
      permission: 'AbpIdentity.Users.Update',
    },
  ]);

  // NB: addMany() only returns a position builder — it adds nothing on its own.
  // addManyTail() is what actually appends (this is what @abp/ng.identity itself uses).
  actionList.addManyTail(actions);
}

// Users grid: manage permissions via roles only — drop the per-user "Permissions" action.
// NB: dropByValue(value, compareFn) — compareFn(item, value) MUST return a boolean. Passing a
// mapper (item => item.text) makes every node "match" and silently drops the FIRST action (Edit).
function removeUserPermissionsAction(actionList: any) {
  actionList.dropByValue('AbpIdentity::Permissions', (action: any, text: string) => action.text === text);
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

  actionList.addManyTail(actions);
}

// Users form: hide the confusing "Account lockout" (lockoutEnabled) field.
// compareFn(item, value) must return a boolean — see removeUserPermissionsAction above.
function hideLockoutFormProp(propList: any) {
  propList.dropByValue('lockoutEnabled', (prop: any, name: string) => prop.name === name);
}

// Roles form: hide the "Default" and "Public" flags (not needed here).
function hideRoleFlagsFormProp(propList: any) {
  propList.dropByValue('isDefault', (prop: any, name: string) => prop.name === name);
  propList.dropByValue('isPublic', (prop: any, name: string) => prop.name === name);
}

// NOTE: these contributors MUST be passed as options to `createRoutes(...)` in app.routes.ts
// (i.e. IdentityModule's `provideIdentity`). The identity module re-provides the contributor
// tokens at the lazy-route injector scope with the options you pass; providing them only at the
// app root gets shadowed by that empty route-scope provider, so the contributors never run.
export const IDENTITY_ENTITY_ACTION_CONTRIBUTORS_VALUE = {
  'Identity.UsersComponent': [
    removeUserPermissionsAction,
    branchesActionContributor,
    resetLoginAttemptsContributor,
  ],
};

export const IDENTITY_CREATE_FORM_PROP_CONTRIBUTORS_VALUE = {
  'Identity.UsersComponent': [hideLockoutFormProp],
  'Identity.RolesComponent': [hideRoleFlagsFormProp],
};

export const IDENTITY_EDIT_FORM_PROP_CONTRIBUTORS_VALUE = {
  'Identity.UsersComponent': [hideLockoutFormProp],
  'Identity.RolesComponent': [hideRoleFlagsFormProp],
};
