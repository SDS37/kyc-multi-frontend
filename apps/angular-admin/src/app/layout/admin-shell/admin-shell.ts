import { Component, OnInit, WritableSignal, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { appRoleLabel, toShellSession } from '../../auth/auth.mappers';
import { ShellSession } from '../../auth/auth.models';
import { TokenStorage } from '../../auth/token-storage';

/**
 * Authenticated chrome for KYC-064: brand, Cases nav, tenant/user, logout.
 * Login stays outside this layout (guest route).
 */
@Component({
  selector: 'app-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule],
  templateUrl: './admin-shell.html',
  styleUrl: './admin-shell.css',
})
export class AdminShell implements OnInit {
  private readonly tokens: TokenStorage = inject(TokenStorage);
  private readonly router: Router = inject(Router);

  private readonly sessionState: WritableSignal<ShellSession | null> = signal(null);

  protected readonly session = computed((): ShellSession | null => this.sessionState());

  protected readonly roleLabel = computed((): string => {
    const current: ShellSession | null = this.sessionState();
    return current ? appRoleLabel(current.role) : '';
  });

  protected readonly tenantLabel = computed((): string => {
    const current: ShellSession | null = this.sessionState();
    if (!current) {
      return '';
    }
    return current.tenantSlug ?? current.tenantId;
  });

  ngOnInit(): void {
    this.sessionState.set(
      toShellSession(this.tokens.getAccessToken(), this.tokens.getTenantSlug()),
    );
  }

  protected signOut(): void {
    this.tokens.clearAccessToken();
    this.sessionState.set(null);
    void this.router.navigateByUrl('/login');
  }
}
