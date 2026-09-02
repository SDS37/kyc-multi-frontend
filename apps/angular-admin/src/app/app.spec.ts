import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { APP_CONFIG, AppConfig } from './config/app-config';

describe('App', () => {
  beforeEach(async (): Promise<void> => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        {
          provide: APP_CONFIG,
          useValue: {
            apiBaseUrl: 'http://localhost:5295',
            graphqlUrl: 'http://localhost:5295/graphql',
            captchaRequiredForLogin: false,
            turnstileSiteKey: '',
          } satisfies AppConfig,
        },
      ],
    }).compileComponents();
  });

  it('should create the app', (): void => {
    const fixture: ComponentFixture<App> = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
