import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DynamicDialogRef, DynamicDialogConfig } from 'primeng/dynamicdialog';

import { RoleBranchService } from '../../proxy/role-branch/role-branch.service';
import { RoleBranchDto } from '../../proxy/role-branch/models';
import { FoodicsService } from '../../proxy/foodics/foodics.service';
import { FoodicsBranchDto } from '../../proxy/application/integrations/foodics/models';

interface AccountGroup {
  accountId: string;
  accountName: string;
  branches: FoodicsBranchDto[];
}

@Component({
  selector: 'app-role-branch-modal',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    ProgressSpinnerModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './role-branch-modal.component.html',
  styleUrls: ['./role-branch-modal.component.scss'],
})
export class RoleBranchModalComponent implements OnInit {
  private roleBranchService = inject(RoleBranchService);
  private foodicsService = inject(FoodicsService);
  private messageService = inject(MessageService);
  private dialogRef = inject(DynamicDialogRef);
  private dialogConfig = inject(DynamicDialogConfig);

  roleId: string = '';
  roleName: string = '';

  loading = false;
  saving = false;

  accountGroups: AccountGroup[] = [];
  selected = new Set<string>(); // key: `${accountId}::${branchId}`
  filter = '';

  /** Groups narrowed by the search box; groups with no match drop out entirely. */
  get visibleGroups(): AccountGroup[] {
    const term = this.filter.trim().toLowerCase();
    if (!term) return this.accountGroups;

    return this.accountGroups
      .map(g => ({
        ...g,
        branches: g.branches.filter(b =>
          `${b.name ?? ''} ${b.name_localized ?? ''}`.toLowerCase().includes(term),
        ),
      }))
      .filter(g => g.branches.length > 0);
  }

  get totalBranches(): number {
    return this.accountGroups.reduce((sum, g) => sum + g.branches.length, 0);
  }

  get selectedCount(): number {
    return this.selected.size;
  }

  allSelectedIn(group: AccountGroup): boolean {
    return group.branches.length > 0
      && group.branches.every(b => this.isSelected(group.accountId, b.id!));
  }

  /** Select-all / clear-all for the branches currently listed under this group. */
  toggleGroup(group: AccountGroup): void {
    const selectAll = !this.allSelectedIn(group);
    for (const b of group.branches) {
      const k = this.key(group.accountId, b.id!);
      if (selectAll) {
        this.selected.add(k);
      } else {
        this.selected.delete(k);
      }
    }
  }

  ngOnInit(): void {
    this.roleId = this.dialogConfig.data?.roleId ?? '';
    this.roleName = this.dialogConfig.data?.roleName ?? '';
    this.loadData();
  }

  private loadData(): void {
    if (!this.roleId) return;

    this.loading = true;

    this.foodicsService.getList({ maxResultCount: 100 }).subscribe({
      next: accounts => {
        const accountList = accounts.items ?? [];

        if (accountList.length === 0) {
          this.loading = false;
          return;
        }

        const branchRequests = accountList.map(account =>
          this.roleBranchService.getAvailableBranches(account.id!).pipe(
            catchError(() => of([] as FoodicsBranchDto[]))
          )
        );

        forkJoin([
          this.roleBranchService.getForRole(this.roleId).pipe(catchError(() => of([] as RoleBranchDto[]))),
          forkJoin(branchRequests),
        ]).subscribe({
          next: ([currentGrants, branchesPerAccount]) => {
            this.selected.clear();
            for (const g of currentGrants) {
              this.selected.add(this.key(g.foodicsAccountId, g.foodicsBranchId));
            }

            this.accountGroups = accountList
              .map((account, i) => ({
                accountId: account.id!,
                accountName: account.brandName || account.oAuthClientId || account.id!,
                branches: branchesPerAccount[i] ?? [],
              }))
              .filter(g => g.branches.length > 0);

            this.loading = false;
          },
          error: () => {
            this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to load branch data.' });
            this.loading = false;
          },
        });
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to load Foodics accounts.' });
        this.loading = false;
      },
    });
  }

  private key(accountId: string, branchId: string): string {
    return `${accountId}::${branchId}`;
  }

  isSelected(accountId: string, branchId: string): boolean {
    return this.selected.has(this.key(accountId, branchId));
  }

  toggle(accountId: string, branchId: string, checked: boolean): void {
    const k = this.key(accountId, branchId);
    if (checked) {
      this.selected.add(k);
    } else {
      this.selected.delete(k);
    }
  }

  save(): void {
    this.saving = true;

    const branches: RoleBranchDto[] = [];
    for (const k of this.selected) {
      const [accountId, branchId] = k.split('::');
      const group = this.accountGroups.find(g => g.accountId === accountId);
      const branch = group?.branches.find(b => b.id === branchId);
      branches.push({
        foodicsAccountId: accountId,
        foodicsBranchId: branchId,
        foodicsBranchName: branch?.name,
      });
    }

    this.roleBranchService.updateForRole(this.roleId, { branches }).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Saved', detail: 'Branch access updated.' });
        this.saving = false;
        this.dialogRef.close(true);
      },
      error: err => {
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: err?.error?.error?.message || 'Failed to save branch access.',
        });
        this.saving = false;
      },
    });
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
