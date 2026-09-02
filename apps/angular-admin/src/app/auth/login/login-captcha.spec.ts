import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { LOGIN_MESSAGES } from '../auth.messages';
import { LoginCaptcha } from './login-captcha';

function requiredTrimmed(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim().length === 0 ? { required: true } : null;
}

@Component({
  selector: 'app-login-captcha-host',
  imports: [ReactiveFormsModule, LoginCaptcha],
  template: `
    <form [formGroup]="form">
      <app-login-captcha formControlName="captchaToken" [siteKey]="siteKey" />
    </form>
  `,
})
class LoginCaptchaHost {
  readonly form: FormGroup<{ captchaToken: FormControl<string> }> = new FormGroup({
    captchaToken: new FormControl('', {
      nonNullable: true,
      validators: [requiredTrimmed, Validators.maxLength(2048)],
    }),
  });
  siteKey: string = '';
}

describe('LoginCaptcha', (): void => {
  let fixture: ComponentFixture<LoginCaptchaHost>;
  let host: LoginCaptchaHost;

  beforeEach(async (): Promise<void> => {
    await TestBed.configureTestingModule({
      imports: [LoginCaptchaHost],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginCaptchaHost);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('writes typed tokens onto the bound form control', (): void => {
    const input: HTMLInputElement = captchaInput(fixture);
    setInputValue(input, ' token-1 ');
    fixture.detectChanges();

    expect(host.form.controls.captchaToken.value).toBe(' token-1 ');
    expect(host.form.controls.captchaToken.valid).toBe(true);
  });

  it('shows the required error after the parent marks the control touched', (): void => {
    host.form.markAllAsTouched();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(LOGIN_MESSAGES.captchaRequired);
  });

  it('clears the token field when the control is reset from the parent', (): void => {
    const input: HTMLInputElement = captchaInput(fixture);
    setInputValue(input, 'token-1');
    fixture.detectChanges();

    host.form.controls.captchaToken.setValue('');
    fixture.detectChanges();

    expect(captchaInput(fixture).value).toBe('');
  });

  it('disables the token field from ControlValueAccessor.setDisabledState', (): void => {
    host.form.controls.captchaToken.disable();
    fixture.detectChanges();

    expect(captchaInput(fixture).disabled).toBe(true);
  });
});

function captchaInput(fixture: ComponentFixture<LoginCaptchaHost>): HTMLInputElement {
  const input: Element | null = fixture.nativeElement.querySelector('input');
  if (!(input instanceof HTMLInputElement)) {
    throw new Error('Expected captcha token input');
  }
  return input;
}

function setInputValue(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input'));
}
