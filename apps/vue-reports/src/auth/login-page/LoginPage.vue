<template>
  <main :class="$style['login']">
    <section :class="$style['panel']" aria-labelledby="login-heading">
      <p :class="$style['brand']">{{ brand }}</p>
      <h1 id="login-heading" :class="$style['title']">{{ copy.title }}</h1>
      <p :class="$style['lede']">{{ copy.lede }}</p>

      <form
        :class="$style['form']"
        novalidate
        :aria-describedby="formError ? 'login-form-error' : undefined"
        @submit.prevent="onSubmit"
      >
        <p
          v-if="formError"
          id="login-form-error"
          :class="$style['alert']"
          role="alert"
          aria-live="polite"
        >
          {{ formError }}
        </p>

        <div :class="$style['field']">
          <label for="tenantSlug">{{ copy.tenantSlugLabel }}</label>
          <input
            id="tenantSlug"
            v-model="tenantSlug"
            name="tenantSlug"
            autocomplete="organization"
            :aria-invalid="touched && fieldErrors.tenantSlug !== undefined"
            :aria-describedby="
              touched && fieldErrors.tenantSlug !== undefined ? 'tenantSlug-error' : undefined
            "
          />
          <p
            v-if="touched && fieldErrors.tenantSlug !== undefined"
            id="tenantSlug-error"
            :class="$style['fieldError']"
          >
            {{ fieldErrors.tenantSlug }}
          </p>
        </div>

        <div :class="$style['field']">
          <label for="email">{{ copy.emailLabel }}</label>
          <input
            id="email"
            v-model="email"
            name="email"
            type="email"
            autocomplete="username"
            :aria-invalid="touched && fieldErrors.email !== undefined"
            :aria-describedby="
              touched && fieldErrors.email !== undefined ? 'email-error' : undefined
            "
          />
          <p
            v-if="touched && fieldErrors.email !== undefined"
            id="email-error"
            :class="$style['fieldError']"
          >
            {{ fieldErrors.email }}
          </p>
        </div>

        <div :class="$style['field']">
          <label for="password">{{ copy.passwordLabel }}</label>
          <input
            id="password"
            v-model="password"
            name="password"
            type="password"
            autocomplete="current-password"
            :aria-invalid="touched && fieldErrors.password !== undefined"
            :aria-describedby="
              touched && fieldErrors.password !== undefined ? 'password-error' : undefined
            "
          />
          <p
            v-if="touched && fieldErrors.password !== undefined"
            id="password-error"
            :class="$style['fieldError']"
          >
            {{ fieldErrors.password }}
          </p>
        </div>

        <LoginCaptcha
          v-if="captchaRequired"
          ref="captchaRef"
          v-model="captchaToken"
          :site-key="turnstileSiteKey"
          :disabled="submitting"
          :invalid="touched && fieldErrors.captchaToken !== undefined"
          @load-failed="onCaptchaLoadFailed"
        />

        <button
          type="submit"
          :class="$style['submit']"
          :disabled="submitting"
          :aria-busy="submitting"
        >
          <template v-if="submitting">
            <span
              :class="$style['spinner']"
              role="status"
              :aria-label="copy.submittingAria"
            />
            <span>{{ copy.submitting }}</span>
          </template>
          <template v-else>{{ copy.submit }}</template>
        </button>
      </form>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, ref, type ComputedRef, type Ref } from 'vue';
import { useRoute, useRouter, type RouteLocationNormalizedLoaded, type Router } from 'vue-router';
import { appConfig } from '../../config/app-config';
import { UI_MESSAGES } from '../../shared/ui.messages';
import {
  hasLoginFieldErrors,
  resolvePostLoginUrl,
  toLoginFailedError,
  validateLoginForm,
} from '../auth.mappers';
import { LOGIN_MESSAGES, type LoginMessages } from '../auth.messages';
import type { LoginCredentials, LoginFieldErrors } from '../auth.models';
import { login } from '../login-api';
import LoginCaptcha from './LoginCaptcha.vue';

defineOptions({ name: 'LoginPage' });

/**
 * Reviewer / TenantAdmin sign-in (KYC-080).
 * Layout and tokens mirror Angular admin / React customer login.
 */
const copy: LoginMessages = LOGIN_MESSAGES;
const brand: string = UI_MESSAGES.brand;
const router: Router = useRouter();
const route: RouteLocationNormalizedLoaded = useRoute();

const tenantSlug: Ref<string> = ref('');
const email: Ref<string> = ref('');
const password: Ref<string> = ref('');
const captchaToken: Ref<string> = ref('');
const touched: Ref<boolean> = ref(false);
const submitting: Ref<boolean> = ref(false);
const formError: Ref<string | null> = ref(null);
const captchaRequired: boolean = appConfig.captchaRequiredForLogin;
const turnstileSiteKey: string = appConfig.turnstileSiteKey;
const captchaRef: Ref<{ reset: () => void } | null> = ref(null);
let submittingLock: boolean = false;

const fieldErrors: ComputedRef<LoginFieldErrors> = computed((): LoginFieldErrors => {
  const credentials: LoginCredentials = {
    tenantSlug: tenantSlug.value,
    email: email.value,
    password: password.value,
    captchaToken: captchaToken.value,
  };
  return validateLoginForm(credentials, { captchaRequired });
});

function onCaptchaLoadFailed(): void {
  formError.value = copy.captchaUnavailable;
}

async function onSubmit(): Promise<void> {
  formError.value = null;
  touched.value = true;

  const credentials: LoginCredentials = {
    tenantSlug: tenantSlug.value,
    email: email.value,
    password: password.value,
    captchaToken: captchaToken.value,
  };
  if (hasLoginFieldErrors(validateLoginForm(credentials, { captchaRequired })) || submittingLock) {
    return;
  }

  submittingLock = true;
  submitting.value = true;
  try {
    await login(credentials);
    submitting.value = false;
    const returnUrlRaw: unknown = route.query['returnUrl'];
    const returnUrl: string | null = typeof returnUrlRaw === 'string' ? returnUrlRaw : null;
    await router.replace(resolvePostLoginUrl(returnUrl));
  } catch (err: unknown) {
    submittingLock = false;
    submitting.value = false;
    captchaRef.value?.reset();
    formError.value = toLoginFailedError(err).message;
  }
}
</script>

<style module>
.login {
  box-sizing: border-box;
  min-height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--kyc-page-gutter);
  background:
    radial-gradient(ellipse 80% 60% at 10% 0%, rgb(15 110 86 / 12%), transparent 55%),
    radial-gradient(ellipse 70% 50% at 90% 100%, rgb(26 35 50 / 6%), transparent 50%),
    var(--kyc-color-surface);
}

.panel {
  width: min(100%, var(--kyc-content-max));
  padding: var(--kyc-space-6);
  background: var(--kyc-color-surface-raised);
  border: 1px solid var(--kyc-color-border);
  border-radius: var(--kyc-radius-lg);
  box-shadow: var(--kyc-shadow-sm);
}

.brand {
  margin: 0 0 var(--kyc-space-2);
  font-size: var(--kyc-text-lg);
  font-weight: 600;
  letter-spacing: 0.02em;
  color: var(--kyc-color-brand);
  line-height: var(--kyc-leading-tight);
}

.title {
  margin: 0 0 var(--kyc-space-2);
  font-size: var(--kyc-text-xl);
  font-weight: 600;
  line-height: var(--kyc-leading-tight);
  color: var(--kyc-color-text);
}

.lede {
  margin: 0 0 var(--kyc-space-5);
  color: var(--kyc-color-text-muted);
  font-size: var(--kyc-text-sm);
}

.form {
  display: flex;
  flex-direction: column;
  gap: var(--kyc-space-3);
}

.field {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: var(--kyc-space-1);
}

.field label {
  font-size: var(--kyc-text-sm);
  font-weight: 600;
  color: var(--kyc-color-text);
}

.field input {
  box-sizing: border-box;
  width: 100%;
  min-height: 2.75rem;
  padding: var(--kyc-space-2) var(--kyc-space-3);
  border: 1px solid var(--kyc-color-border);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-surface-raised);
  color: var(--kyc-color-text);
  font: inherit;
  font-size: var(--kyc-text-md);
}

.field input:focus-visible {
  outline: none;
  box-shadow: var(--kyc-focus-ring);
  border-color: var(--kyc-color-focus);
}

.field input[aria-invalid='true'] {
  border-color: var(--kyc-color-danger);
}

.fieldError {
  margin: 0;
  color: var(--kyc-color-danger);
  font-size: var(--kyc-text-sm);
}

.alert {
  margin: 0;
  padding: var(--kyc-space-3) var(--kyc-space-4);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-danger-bg);
  color: var(--kyc-color-danger);
  font-size: var(--kyc-text-sm);
  border: 1px solid color-mix(in srgb, var(--kyc-color-danger) 25%, transparent);
}

.submit {
  align-self: stretch;
  margin-top: var(--kyc-space-1);
  min-height: 2.75rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--kyc-space-2);
  border: none;
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-brand);
  color: var(--kyc-color-on-brand);
  font: inherit;
  font-size: var(--kyc-text-md);
  font-weight: 600;
  cursor: pointer;
}

.submit:hover:not(:disabled) {
  background: var(--kyc-color-brand-hover);
}

.submit:disabled {
  opacity: 0.75;
  cursor: not-allowed;
}

.submit:focus-visible {
  outline: none;
  box-shadow: var(--kyc-focus-ring);
}

.spinner {
  width: 1.25rem;
  height: 1.25rem;
  border: 2px solid rgb(255 255 255 / 35%);
  border-top-color: var(--kyc-color-on-brand);
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 640px) {
  .panel {
    padding: var(--kyc-space-5);
  }
}
</style>
