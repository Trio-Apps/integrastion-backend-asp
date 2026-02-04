import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationModule } from '@abp/ng.core';

@Component({
  selector: 'abp-privacy-policy',
  standalone: true,
  imports: [CommonModule, LocalizationModule],
  templateUrl: './privacy-policy.component.html',
})
export class PrivacyPolicyComponent {}
