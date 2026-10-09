import { describe, expect, it } from 'vitest';
import {
  assertProductionApiConfig,
  isLocalhostApiUrl,
  resolveConfiguredUrl,
} from './production-api-url';

const devApi: string = 'http://localhost:5295';
const devGraphql: string = 'http://localhost:5295/graphql';
const deployedApi: string = 'https://api.example.com';
const deployedGraphql: string = 'https://api.example.com/graphql';

describe('resolveConfiguredUrl', (): void => {
  it('keeps the development localhost default when env is empty', (): void => {
    expect(resolveConfiguredUrl(undefined, devApi, false)).toBe(devApi);
    expect(resolveConfiguredUrl('   ', devApi, false)).toBe(devApi);
  });

  it('keeps an explicit development URL', (): void => {
    expect(resolveConfiguredUrl(deployedApi, devApi, false)).toBe(deployedApi);
  });

  it('does not substitute localhost when production env is empty', (): void => {
    expect(resolveConfiguredUrl(undefined, devApi, true)).toBe('');
    expect(resolveConfiguredUrl('   ', devApi, true)).toBe('');
    expect(resolveConfiguredUrl(`  ${devApi}  `, devApi, true)).toBe(devApi);
  });
});

describe('assertProductionApiConfig', (): void => {
  it('allows Development localhost URLs', (): void => {
    expect((): void => {
      assertProductionApiConfig({
        production: false,
        apiBaseUrl: devApi,
        graphqlUrl: devGraphql,
      });
    }).not.toThrow();
  });

  it('rejects an empty production API URL on its own', (): void => {
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: '   ',
        graphqlUrl: deployedGraphql,
      });
    }).toThrow(/must be set/);
  });

  it('rejects an empty production GraphQL URL on its own', (): void => {
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: deployedApi,
        graphqlUrl: '',
      });
    }).toThrow(/must be set/);
  });

  it('rejects a production localhost API URL when GraphQL is deployed', (): void => {
    expect(isLocalhostApiUrl('http://LOCALHOST:5295')).toBe(true);
    expect(isLocalhostApiUrl('https://localhost.example.com')).toBe(false);
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: 'http://LOCALHOST:5295',
        graphqlUrl: deployedGraphql,
      });
    }).toThrow(/must not point at localhost/);
  });

  it('rejects a production loopback GraphQL URL when the API is deployed', (): void => {
    expect(isLocalhostApiUrl('http://127.0.0.1:5295')).toBe(true);
    expect(isLocalhostApiUrl('http://127.1:5295')).toBe(true);
    expect(isLocalhostApiUrl('http://[::1]:5295/graphql')).toBe(true);
    expect(isLocalhostApiUrl('http://[0:0:0:0:0:0:0:1]:5295/')).toBe(true);
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: deployedApi,
        graphqlUrl: 'http://127.0.0.1:5295/graphql',
      });
    }).toThrow(/must not point at localhost/);
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: deployedApi,
        graphqlUrl: 'http://[::1]:5295/graphql',
      });
    }).toThrow(/must not point at localhost/);
  });

  it('allows an explicit HTTPS production origin', (): void => {
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: deployedApi,
        graphqlUrl: deployedGraphql,
      });
    }).not.toThrow();
  });
});
