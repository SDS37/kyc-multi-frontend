import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { TokenStorage } from '../../auth/token-storage';

/**
 * Stub case list landing after login (KYC-061). Full list UI is KYC-062.
 */
@Component({
  selector: 'app-case-list',
  imports: [MatButtonModule],
  templateUrl: './case-list.html',
  styleUrl: './case-list.css',
})
export class CaseList {
  private readonly tokens: TokenStorage = inject(TokenStorage);
  private readonly router: Router = inject(Router);

  protected readonly title: string = 'Cases';

  protected signOut(): void {
    this.tokens.clearAccessToken();
    void this.router.navigateByUrl('/login');
  }
}
