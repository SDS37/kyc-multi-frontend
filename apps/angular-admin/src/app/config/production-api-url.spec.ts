import { describe, expect, it } from 'vitest';
import { assertProductionApiConfig, isLocalhostApiUrl } from './production-api-url';

describe('assertProductionApiConfig', (): void => {
  it('allows Development localhost URLs', (): void => {
    expect((): void => {
      assertProductionApiConfig({
        production: false,
        apiBaseUrl: 'http://localhost:5295',
        graphqlUrl: 'http://localhost:5295/graphql',
      });
    }).not.toThrow();
  });

  it('rejects empty production URLs', (): void => {
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: '',
        graphqlUrl: '',
      });
    }).toThrow(/must be set/);
  });

  it('rejects production localhost URLs', (): void => {
    expect(isLocalhostApiUrl('http://localhost:5295')).toBe(true);
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: 'http://localhost:5295',
        graphqlUrl: 'http://localhost:5295/graphql',
      });
    }).toThrow(/must not point at localhost/);
  });

  it('allows an explicit HTTPS production origin', (): void => {
    expect((): void => {
      assertProductionApiConfig({
        production: true,
        apiBaseUrl: 'https://api.example.com',
        graphqlUrl: 'https://api.example.com/graphql',
      });
    }).not.toThrow();
  });
});
