import { Component, inject, OnInit } from '@angular/core';
import { AuthService, LocalizationPipe, RoutesService } from '@abp/ng.core';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  imports: [LocalizationPipe]
})
export class HomeComponent implements OnInit {
  service = inject(RoutesService);
  ngOnInit(): void {
  }
  private authService = inject(AuthService);

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated
  }

  login() {
    this.authService.navigateToLogin();
  }
}
