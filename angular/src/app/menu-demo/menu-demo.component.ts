import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TabsModule } from 'primeng/tabs';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { LocalizationPipe } from '@abp/ng.core';
import { MenuSyncService } from '@proxy/background-jobs';
import type { StagingMenuGroupSummaryDto } from '@proxy/background-jobs/models';
import type { GetStagingMenuGroupSummaryRequest } from '@proxy/background-jobs/models';

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    TableModule, 
    InputTextModule, 
    ButtonModule,
    CardModule,
    TabsModule,
    CheckboxModule,
    TagModule,
    LocalizationPipe
  ],
  templateUrl: './menu-demo.component.html'
})
export class MenuDemoComponent implements OnInit {

  // DB (staging) menu group summary
  stagingMenuGroups: StagingMenuGroupSummaryDto[] = [];
  
  // UI state
  error = '';
  isLoading = false;

  constructor(private menuSync: MenuSyncService) {}

  ngOnInit(): void {
    this.loadEnhanced();
  }

  toJson(value: unknown): string {
    try {
      return JSON.stringify(value, null, 2);
    } catch {
      return String(value);
    }
  }

  loadEnhanced() {
    this.error = '';
    this.isLoading = true;
    
    const request: GetStagingMenuGroupSummaryRequest = {
      foodicsAccountId: undefined, // Use current tenant's account
      branchId: undefined
    };

    // Load per-menu-group summary from staging table (AppFoodicsProductStaging)
    this.menuSync.getStagingMenuGroupSummary(request).subscribe({
      next: groups => {
        this.stagingMenuGroups = groups ?? [];
        this.error = '';
      },
      error: err => {
        this.error = formatHttpError(err);
        this.stagingMenuGroups = [];
      }
    }).add(() => {
      this.isLoading = false;
    });
  }

}

function formatHttpError(err: any): string {
  try {
    if (err?.error) {
      return JSON.stringify(err.error, null, 2);
    }
    return err?.message ?? String(err);
  } catch {
    return String(err);
  }
}


