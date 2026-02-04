import { Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationModule } from '@abp/ng.core';

@Component({
  selector: 'app-tenant-dashboard',
  standalone: true,
  imports: [CommonModule, LocalizationModule],
  templateUrl: './tenant-dashboard.component.html',
  styleUrls: ['./tenant-dashboard.component.scss'],
})
export class TenantDashboardComponent implements OnDestroy {

  ngOnDestroy(): void {}
}
