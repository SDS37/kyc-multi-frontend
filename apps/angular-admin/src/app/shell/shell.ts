import { Component, inject } from '@angular/core';
import { APP_CONFIG } from '../config/app-config';
import { TokenStorage } from '../auth/token-storage';

/**
 * Temporary shell home (KYC-060). Login and case features land in KYC-061–064.
 */
@Component({
  selector: 'app-shell',
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  private readonly config = inject(APP_CONFIG);
  private readonly tokenStorage = inject(TokenStorage);

  protected readonly title = 'KYC Admin';
  protected readonly graphqlUrl = this.config.graphqlUrl;
  protected readonly apiBaseUrl = this.config.apiBaseUrl;
  protected readonly hasToken = this.tokenStorage.getAccessToken() !== null;
}
