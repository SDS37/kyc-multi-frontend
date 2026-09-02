import type { GraphqlResponse } from '../shared/graphql.models';
import * as http from '../shared/http';
import { LOGIN_MESSAGES } from './auth.messages';
import {
  parseLoginSuccess,
  toLoginFailedError,
  toLoginMutationInput,
  toShellSession,
} from './auth.mappers';
import {
  LoginFailedError,
  isReportsRole,
  type LoginCredentials,
  type LoginMutationInput,
  type LoginSuccess,
  type ShellSession,
} from './auth.models';
import { tokenStorage } from './token-storage';

const LOGIN_MUTATION: string = `
  mutation Login($input: LoginRequestInput!) {
    login(input: $input) {
      accessToken
      tokenType
      expiresInSeconds
    }
  }
`;

/**
 * Anonymous GraphQL `login` (KYC-080) — same contract as Angular / React.
 * Pure parse/normalize in auth.mappers; token write is the intentional side effect.
 */
export async function login(credentials: LoginCredentials): Promise<LoginSuccess> {
  const input: LoginMutationInput = toLoginMutationInput(credentials);

  try {
    const body: GraphqlResponse<{ login?: LoginSuccess | null }> = await http.graphqlRequest<
      { login?: LoginSuccess | null }
    >(LOGIN_MUTATION, { input }, { skipAuth: true });
    const success: LoginSuccess = parseLoginSuccess(body);
    const session: ShellSession | null = toShellSession(success.accessToken, input.tenantSlug);
    if (session === null || !isReportsRole(session.role)) {
      throw new LoginFailedError(LOGIN_MESSAGES.wrongAppRole, 'AUTH_NOT_AUTHORIZED');
    }
    tokenStorage.setSession(success.accessToken, input.tenantSlug);
    return success;
  } catch (err: unknown) {
    throw toLoginFailedError(err);
  }
}
