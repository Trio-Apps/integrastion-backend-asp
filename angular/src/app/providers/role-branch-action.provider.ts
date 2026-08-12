import { provideAppInitializer } from '@angular/core';
import { DialogService } from 'primeng/dynamicdialog';
import { EntityAction } from '@abp/ng.components/extensible';
import { IDENTITY_ENTITY_ACTION_CONTRIBUTORS } from '@abp/ng.identity';
import { IdentityRoleDto } from '@abp/ng.identity/proxy';
import { RoleBranchModalComponent } from '../identity/role-branch-modal/role-branch-modal.component';

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

export const ROLE_BRANCH_ACTION_PROVIDER = [
  {
    provide: IDENTITY_ENTITY_ACTION_CONTRIBUTORS,
    useValue: {
      'Identity.RolesComponent': [branchesActionContributor],
    },
  },
];
