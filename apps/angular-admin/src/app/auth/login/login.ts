import { Component, DestroyRef, WritableSignal, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { APP_CONFIG, AppConfig } from '../../config/app-config';
import { resolvePostLoginUrl, toLoginFailedError } from '../auth.mappers';
import { LOGIN_MESSAGES } from '../auth.messages';
import { LoginFormControls } from '../auth.models';
import { LoginService } from '../login.service';
import { UI_MESSAGES } from '../../shared/ui.messages';
import { LoginCaptcha } from './login-captcha';

function requiredTrimmed(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim().length === 0 ? { required: true } : null;
}

/**
 * Admin / reviewer sign-in (KYC-061 / KYC-094).
 * Fields: tenant slug, email, password, optional captcha → GraphQL `login` → TokenStorage → /cases.
 */
@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    LoginCaptcha,
  ],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly loginService: LoginService = inject(LoginService);
  private readonly router: Router = inject(Router);
  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly destroyRef: DestroyRef = inject(DestroyRef);
  private readonly config: AppConfig = inject(APP_CONFIG);

  protected readonly copy: typeof LOGIN_MESSAGES = LOGIN_MESSAGES;
  protected readonly brand: string = UI_MESSAGES.brand;
  protected readonly captchaRequired: boolean = this.config.captchaRequiredForLogin === true;
  protected readonly turnstileSiteKey: string = this.config.turnstileSiteKey?.trim() ?? '';

  protected readonly submitting: WritableSignal<boolean> = signal(false);
  protected readonly formError: WritableSignal<string | null> = signal(null);

  protected readonly form: FormGroup<LoginFormControls> = this.fb.nonNullable.group({
    tenantSlug: ['', [Validators.required, Validators.maxLength(64)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
    captchaToken: [
      '',
      this.captchaRequired
        ? [requiredTrimmed, Validators.maxLength(2048)]
        : [Validators.maxLength(2048)],
    ],
  });

  protected onCaptchaLoadFailed(): void {
    this.formError.set(this.copy.captchaUnavailable);
  }

  protected submit(): void {
    this.formError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.setCaptchaEnabled(false);
    const {
      tenantSlug,
      email,
      password,
      captchaToken,
    }: { tenantSlug: string; email: string; password: string; captchaToken: string } =
      this.form.getRawValue();

    this.loginService
      .login({ tenantSlug, email, password, captchaToken })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (): void => {
          // Reset before navigate so a delayed/failed redirect does not leave the CTA disabled.
          this.submitting.set(false);
          this.setCaptchaEnabled(true);
          const returnUrl: string | null = this.route.snapshot.queryParamMap.get('returnUrl');
          void this.router.navigateByUrl(resolvePostLoginUrl(returnUrl));
        },
        error: (err: unknown): void => {
          this.submitting.set(false);
          this.form.controls.captchaToken.setValue('');
          this.setCaptchaEnabled(true);
          this.formError.set(toLoginFailedError(err).message);
        },
      });
  }

  private setCaptchaEnabled(enabled: boolean): void {
    if (!this.captchaRequired) {
      return;
    }
    const captcha: FormControl<string> = this.form.controls.captchaToken;
    if (enabled) {
      captcha.enable({ emitEvent: false });
      return;
    }
    captcha.disable({ emitEvent: false });
  }
}
