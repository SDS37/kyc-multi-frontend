import {
  AfterViewInit,
  Component,
  ElementRef,
  Injector,
  OnDestroy,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { LOGIN_MESSAGES } from '../auth.messages';
import { loadTurnstileWidget, type TurnstileWidgetApi } from '../turnstile-loader';

/**
 * Presentational login captcha (KYC-094).
 * Turnstile widget when a site key is set; otherwise a labeled token field (API `test` provider).
 */
@Component({
  selector: 'app-login-captcha',
  imports: [MatFormFieldModule, MatInputModule],
  templateUrl: './login-captcha.html',
  styleUrl: './login-captcha.css',
})
export class LoginCaptcha implements AfterViewInit, OnDestroy {
  readonly siteKey = input<string>('');
  readonly disabled = input<boolean>(false);
  readonly invalid = input<boolean>(false);
  readonly tokenChange = output<string>();
  readonly loadFailed = output<void>();

  protected readonly copy: typeof LOGIN_MESSAGES = LOGIN_MESSAGES;
  protected readonly usesWidget = computed((): boolean => this.siteKey().trim().length > 0);
  private readonly injector: Injector = inject(Injector);
  private readonly widgetHost = viewChild<ElementRef<HTMLDivElement>>('widgetHost');

  private widgetApi: TurnstileWidgetApi | null = null;
  private widgetId: string | null = null;
  private destroyed: boolean = false;

  ngAfterViewInit(): void {
    afterNextRender(
      (): void => {
        const siteKey: string = this.siteKey().trim();
        if (!siteKey) {
          return;
        }
        void this.mountWidget(siteKey);
      },
      { injector: this.injector },
    );
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.removeWidget();
  }

  resetWidget(): void {
    if (this.widgetApi && this.widgetId) {
      this.widgetApi.reset(this.widgetId);
    }
    this.tokenChange.emit('');
  }

  protected onTokenInput(event: Event): void {
    const target: EventTarget | null = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }
    this.tokenChange.emit(target.value);
  }

  private async mountWidget(siteKey: string): Promise<void> {
    try {
      const api: TurnstileWidgetApi = await loadTurnstileWidget();
      if (this.destroyed) {
        return;
      }
      const host: HTMLDivElement | undefined = this.widgetHost()?.nativeElement;
      if (!host) {
        this.loadFailed.emit();
        return;
      }
      this.widgetApi = api;
      this.widgetId = api.render(host, {
        sitekey: siteKey,
        callback: (token: string): void => {
          this.tokenChange.emit(token);
        },
        'expired-callback': (): void => {
          this.tokenChange.emit('');
        },
        'error-callback': (): void => {
          this.tokenChange.emit('');
        },
        theme: 'auto',
      });
    } catch {
      if (!this.destroyed) {
        this.loadFailed.emit();
      }
    }
  }

  private removeWidget(): void {
    if (this.widgetApi && this.widgetId) {
      this.widgetApi.remove(this.widgetId);
    }
    this.widgetApi = null;
    this.widgetId = null;
  }
}
