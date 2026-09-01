import { toShellSession } from './auth.mappers';
import type { ShellSession } from './auth.models';
import { tokenStorage } from './token-storage';

/**
 * Valid display session, or null.
 * Clears corrupt JWTs so guards treat them as logged-out.
 */
export function getValidShellSession(): ShellSession | null {
  const accessToken: string | null = tokenStorage.getAccessToken();
  if (!accessToken) {
    return null;
  }

  const session: ShellSession | null = toShellSession(
    accessToken,
    tokenStorage.getTenantSlug(),
  );
  if (!session) {
    tokenStorage.clearSession();
    return null;
  }

  return session;
}
