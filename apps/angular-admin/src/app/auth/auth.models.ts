import { FormControl } from '@angular/forms';
import { GraphqlError } from '../shared/graphql.models';

export interface LoginCredentials {
  tenantSlug: string;
  email: string;
  password: string;
}

export interface LoginSuccess {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
}

/** Reactive form controls for the login page. */
export interface LoginFormControls {
  tenantSlug: FormControl<string>;
  email: FormControl<string>;
  password: FormControl<string>;
}

/** User-facing login failure (validation, AUTH_FAILED, or transport). */
export class LoginFailedError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'LoginFailedError';
  }
}

/** Wire shape for GraphQL `login` HTTP body (auth feature only). */
export interface GraphqlLoginBody {
  data?: { login?: LoginSuccess | null };
  errors?: GraphqlError[];
}
