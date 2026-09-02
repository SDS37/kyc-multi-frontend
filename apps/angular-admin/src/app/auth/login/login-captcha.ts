import { DOCUMENT } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  OnDestroy,
  OnInit,
  WritableSignal,
  afterRenderEffect,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
  type EffectCleanupRegisterFn,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, ControlValueAccessor, NgControl } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { LOGIN_MESSAGES } from '../auth.messages';
import { loadTurnstileWidget, type TurnstileWidgetApi } from '../turnstile-loader';

/**
 * Login captcha as a Reactive Forms control (KYC-094).
 * Turnstile widget when a site key is set; otherwise a labeled token field (API `test` provider).
 */
@Component({
  selector: 'app-login-captcha',
  imports: [MatFormFieldModule, MatInputModule],
  templateUrl: './login-captcha.html',
  styleUrl: './login-captcha.css',
})
export class LoginCaptcha implements ControlValueAccessor, OnInit, OnDestroy {
  readonly siteKey = input<string>('');
  readonly loadFailed = output<void>();

  protected readonly copy: typeof LOGIN_MESSAGES = LOGIN_MESSAGES;
  protected readonly usesWidget = computed((): boolean => this.siteKey().trim().length > 0);
  protected readonly token: WritableSignal<string> = signal('');
  protected readonly disabled: WritableSignal<boolean> = signal(false);
  protected readonly showInvalid: WritableSignal<boolean> = signal(false);

  private readonly document: Document = inject(DOCUMENT);
  private readonly destroyRef: DestroyRef = inject(DestroyRef);
  private readonly ngControl: NgControl | null = inject(NgControl, {
    optional: true,
    self: true,
  });
  private readonly widgetHost = viewChild<ElementRef<HTMLDivElement>>('widgetHost');

  private widgetApi: TurnstileWidgetApi | null = null;
  private widgetId: string | null = null;
  private destroyed: boolean = false;
  private onChange: (value: string) => void = (): void => {
    /* CVA: assigned in registerOnChange */
  };
  private onTouched: () => void = (): void => {
    /* CVA: assigned in registerOnTouched */
  };

  constructor() {
    // CVA wiring only: providing NG_VALUE_ACCESSOR on the same node as NgControl is a cycle.
    const ngControl: NgControl | null = this.ngControl;
    if (ngControl !== null) {
      ngControl.valueAccessor = this;
    }
  }

  /**
   * Signal-tracked, after Angular has committed the view.
   * Reads `siteKey` / `widgetHost`; writes the Turnstile widget into the host.
   */
  private readonly widgetRender = afterRenderEffect({
    write: (onCleanup: EffectCleanupRegisterFn): void => {
      const siteKey: string = this.siteKey().trim();
      const host: HTMLDivElement | undefined = this.widgetHost()?.nativeElement;
      if (!siteKey || !host) {
        return;
      }

      void this.mountWidget(siteKey, host);
      onCleanup((): void => {
        this.removeWidget();
      });
    },
  });

  ngOnInit(): void {
    const control: AbstractControl | null = this.ngControl?.control ?? null;
    if (control === null) {
      return;
    }
    control.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((): void => {
      this.syncErrorState();
    });
  }

  ngOnDestroy(): void {
    this.widgetRender.destroy();
    this.destroyed = true;
    this.removeWidget();
  }

  writeValue(value: string | null): void {
    const next: string = value ?? '';
    this.token.set(next);
    if (next.length === 0) {
      this.resetTurnstile();
    }
    this.syncErrorState();
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  protected onTokenInput(event: Event): void {
    const target: EventTarget | null = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }
    this.commitToken(target.value);
  }

  protected markTouched(): void {
    this.onTouched();
    this.syncErrorState();
  }

  private commitToken(token: string): void {
    if (this.destroyed || this.disabled()) {
      return;
    }
    this.token.set(token);
    this.onChange(token);
    this.onTouched();
    this.syncErrorState();
  }

  private syncErrorState(): void {
    const control: AbstractControl | null = this.ngControl?.control ?? null;
    this.showInvalid.set(control !== null && control.invalid && control.touched);
  }

  private async mountWidget(siteKey: string, host: HTMLDivElement): Promise<void> {
    try {
      const api: TurnstileWidgetApi = await loadTurnstileWidget(this.document);
      if (this.destroyed || this.widgetHost()?.nativeElement !== host) {
        return;
      }
      this.widgetApi = api;
      this.widgetId = api.render(host, {
        sitekey: siteKey,
        callback: (token: string): void => {
          this.commitToken(token);
        },
        'expired-callback': (): void => {
          this.commitToken('');
        },
        'error-callback': (): void => {
          this.commitToken('');
        },
        theme: 'auto',
      });
    } catch {
      if (!this.destroyed) {
        this.loadFailed.emit();
      }
    }
  }

  private resetTurnstile(): void {
    if (this.widgetApi && this.widgetId) {
      this.widgetApi.reset(this.widgetId);
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
