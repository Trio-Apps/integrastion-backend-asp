import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationModule } from '@abp/ng.core';

@Component({
  selector: 'abp-cookie-policy',
  standalone: true,
  imports: [CommonModule, LocalizationModule],
  templateUrl: './cookie-policy.component.html',
})
export class CookiePolicyComponent {}
