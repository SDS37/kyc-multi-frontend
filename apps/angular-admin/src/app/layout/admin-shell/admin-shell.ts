import { Component, DestroyRef, OnInit, WritableSignal, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { appRoleLabel, toShellSession } from '../../auth/auth.mappers';
import { ShellSession } from '../../auth/auth.models';
import { TokenStorage } from '../../auth/token-storage';
import { SHELL_MESSAGES, tenantIdTitle } from '../shell.messages';

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
  private readonly destroyRef: DestroyRef = inject(DestroyRef);

  private readonly sessionState: WritableSignal<ShellSession | null> = signal(null);

  protected readonly copy: typeof SHELL_MESSAGES = SHELL_MESSAGES;

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

  protected readonly tenantTitle = computed((): string => {
    const current: ShellSession | null = this.sessionState();
    return current ? tenantIdTitle(current.tenantId) : '';
  });

  ngOnInit(): void {
    this.syncSession();
    this.router.events
      .pipe(
        filter((event: unknown): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((): void => {
        this.syncSession();
      });
  }

  protected signOut(): void {
    this.tokens.clearSession();
    this.sessionState.set(null);
    void this.router.navigateByUrl('/login');
  }

  private syncSession(): void {
    this.sessionState.set(
      toShellSession(this.tokens.getAccessToken(), this.tokens.getTenantSlug()),
    );
  }
}
