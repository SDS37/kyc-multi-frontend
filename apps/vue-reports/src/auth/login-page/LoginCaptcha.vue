<template>
  <div :class="$style['captcha']">
    <template v-if="usesWidget">
      <p :class="$style['label']" id="login-captcha-label">{{ copy.captchaLabel }}</p>
      <div
        ref="hostEl"
        :class="$style['widget']"
        role="group"
        aria-labelledby="login-captcha-label"
        :aria-invalid="invalid || undefined"
        :aria-describedby="invalid ? 'captcha-error' : undefined"
      />
      <p v-if="invalid" id="captcha-error" :class="$style['error']">
        {{ copy.captchaRequired }}
      </p>
    </template>
    <template v-else>
      <label for="captchaToken">{{ copy.captchaLabel }}</label>
      <input
        id="captchaToken"
        :value="modelValue"
        name="captchaToken"
        type="text"
        autocomplete="off"
        spellcheck="false"
        maxlength="2048"
        :disabled="disabled"
        :aria-invalid="invalid"
        :aria-describedby="invalid ? 'captcha-error' : 'captcha-help'"
        @input="onInput"
      />
      <p id="captcha-help" :class="$style['help']">{{ copy.captchaHelp }}</p>
      <p v-if="invalid" id="captcha-error" :class="$style['error']">
        {{ copy.captchaRequired }}
      </p>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, type ComputedRef, type Ref } from 'vue';
import { LOGIN_MESSAGES, type LoginMessages } from '../auth.messages';
import { loadTurnstileWidget, type TurnstileWidgetApi } from '../turnstile-loader';

defineOptions({ name: 'LoginCaptcha' });

const props = defineProps<{
  readonly siteKey: string;
  readonly disabled: boolean;
  readonly invalid: boolean;
  readonly modelValue: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [token: string];
  loadFailed: [];
}>();

const copy: LoginMessages = LOGIN_MESSAGES;
const usesWidget: ComputedRef<boolean> = computed((): boolean => props.siteKey.trim().length > 0);
const hostEl: Ref<HTMLDivElement | null> = ref(null);
let widgetApi: TurnstileWidgetApi | null = null;
let widgetId: string | null = null;
let destroyed: boolean = false;

function onInput(event: Event): void {
  const target: EventTarget | null = event.target;
  if (!(target instanceof HTMLInputElement)) {
    return;
  }
  emit('update:modelValue', target.value);
}

function reset(): void {
  if (widgetApi && widgetId) {
    widgetApi.reset(widgetId);
  }
  emit('update:modelValue', '');
}

defineExpose({ reset });

onMounted((): void => {
  if (!usesWidget.value) {
    return;
  }
  const site: string = props.siteKey.trim();
  void loadTurnstileWidget()
    .then((api: TurnstileWidgetApi): void => {
      if (destroyed) {
        return;
      }
      const host: HTMLDivElement | null = hostEl.value;
      if (!host) {
        emit('loadFailed');
        return;
      }
      widgetApi = api;
      widgetId = api.render(host, {
        sitekey: site,
        callback: (token: string): void => {
          emit('update:modelValue', token);
        },
        'expired-callback': (): void => {
          emit('update:modelValue', '');
        },
        'error-callback': (): void => {
          emit('update:modelValue', '');
        },
        theme: 'auto',
      });
    })
    .catch((): void => {
      if (!destroyed) {
        emit('loadFailed');
      }
    });
});

onUnmounted((): void => {
  destroyed = true;
  if (widgetApi && widgetId) {
    widgetApi.remove(widgetId);
  }
  widgetApi = null;
  widgetId = null;
});
</script>

<style module>
.captcha {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: var(--kyc-space-1);
}

.label,
.captcha label {
  margin: 0;
  font-size: var(--kyc-text-sm);
  font-weight: 600;
  color: var(--kyc-color-text);
}

.captcha input {
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

.captcha input:focus-visible {
  outline: none;
  box-shadow: var(--kyc-focus-ring);
  border-color: var(--kyc-color-focus);
}

.captcha input[aria-invalid='true'] {
  border-color: var(--kyc-color-danger);
}

.help {
  margin: 0;
  color: var(--kyc-color-text-muted);
  font-size: var(--kyc-text-sm);
}

.widget {
  min-height: 65px;
  overflow-x: auto;
}

.error {
  margin: 0;
  color: var(--kyc-color-danger);
  font-size: var(--kyc-text-sm);
}
</style>
