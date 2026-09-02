import {
  DEMO_CUSTOMER_EMAIL,
  DEMO_PASSWORD,
  DEMO_TENANT_SLUG,
} from './demo-accounts';

const API_BASE_URL: string = process.env['E2E_API_BASE_URL'] ?? 'http://localhost:5295';

const DEMO_PNG: Buffer = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

const COMPLETE_FORM_DATA: string =
  '{"fullName":"Ada Lovelace","dateOfBirth":"1815-12-10","nationality":"British","address":"12 Analytical Engine Rd"}';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function readString(record: Record<string, unknown>, key: string): string {
  const value: unknown = record[key];
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`Expected non-empty string at "${key}".`);
  }
  return value;
}

function nestedRecord(record: Record<string, unknown>, key: string): Record<string, unknown> {
  const value: unknown = record[key];
  if (!isRecord(value)) {
    throw new Error(`Expected object at "${key}".`);
  }
  return value;
}

async function graphqlData(
  query: string,
  variables: Record<string, unknown>,
  token: string | null,
): Promise<Record<string, unknown>> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token !== null) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response: Response = await fetch(`${API_BASE_URL}/graphql`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ query, variables }),
  });
  if (!response.ok) {
    throw new Error(`GraphQL HTTP ${String(response.status)}`);
  }

  const body: unknown = await response.json();
  if (!isRecord(body)) {
    throw new Error('GraphQL body was not an object.');
  }
  const rawErrors: unknown = body['errors'];
  if (Array.isArray(rawErrors) && rawErrors.length > 0) {
    const first: unknown = rawErrors[0];
    const message: string =
      isRecord(first) && typeof first['message'] === 'string'
        ? first['message']
        : 'GraphQL error';
    throw new Error(message);
  }
  const data: unknown = body['data'];
  if (!isRecord(data)) {
    throw new Error('GraphQL data was missing.');
  }
  return data;
}

async function customerAccessToken(): Promise<string> {
  const data: Record<string, unknown> = await graphqlData(
    `mutation Login($input: LoginRequestInput!) {
      login(input: $input) { accessToken }
    }`,
    {
      input: {
        tenantSlug: DEMO_TENANT_SLUG,
        email: DEMO_CUSTOMER_EMAIL,
        password: DEMO_PASSWORD,
      },
    },
    null,
  );
  return readString(nestedRecord(data, 'login'), 'accessToken');
}

/**
 * Creates a unique submitted case with a PNG so the review smoke does not
 * depend on leftover `[seed] Submitted` status from a previous local run.
 */
export async function prepareSubmittedCase(): Promise<{
  readonly title: string;
  readonly documentFileName: string;
}> {
  const title: string = `[e2e] Review ${String(Date.now())}`;
  const documentFileName: string = 'e2e-id.png';
  const token: string = await customerAccessToken();

  const created: Record<string, unknown> = await graphqlData(
    `mutation CreateDraftCase($input: CreateDraftCaseRequestInput!) {
      createDraftCase(input: $input) { id }
    }`,
    { input: { title } },
    token,
  );
  const caseId: string = readString(nestedRecord(created, 'createDraftCase'), 'id');

  await graphqlData(
    `mutation UpdateDraftCase($input: UpdateDraftCaseRequestInput!) {
      updateDraftCase(input: $input) { id }
    }`,
    {
      input: {
        id: caseId,
        title,
        formData: COMPLETE_FORM_DATA,
      },
    },
    token,
  );

  const uploadBody: FormData = new FormData();
  uploadBody.append(
    'file',
    new Blob([new Uint8Array(DEMO_PNG)], { type: 'image/png' }),
    documentFileName,
  );
  const uploadResponse: Response = await fetch(
    `${API_BASE_URL}/api/cases/${encodeURIComponent(caseId)}/documents`,
    {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
      body: uploadBody,
    },
  );
  if (!uploadResponse.ok) {
    throw new Error(`Document upload HTTP ${String(uploadResponse.status)}`);
  }

  await graphqlData(
    `mutation SubmitCase($input: SubmitCaseRequestInput!) {
      submitCase(input: $input) { id status }
    }`,
    { input: { id: caseId } },
    token,
  );

  return { title, documentFileName };
}
