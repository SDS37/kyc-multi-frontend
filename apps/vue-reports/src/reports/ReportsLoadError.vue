<template>
  <div :class="$style['alert']" role="alert">
    <p>{{ message }}</p>
    <button type="button" :class="$style['retry']" @click="$emit('retry')">
      {{ tryAgain }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { UI_MESSAGES } from '../shared/ui.messages';

defineOptions({ name: 'ReportsLoadError' });

defineProps<{
  readonly message: string;
}>();

defineEmits<{
  retry: [];
}>();

const tryAgain: string = UI_MESSAGES.tryAgain;
</script>

<style module>
.alert {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--kyc-space-3);
  margin: var(--kyc-space-4) 0;
  padding: var(--kyc-space-3) var(--kyc-space-4);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-danger-bg);
  color: var(--kyc-color-danger);
  border: 1px solid var(--kyc-color-border);
  font-size: var(--kyc-text-sm);
}

.alert p {
  margin: 0;
  flex: 1 1 12rem;
}

.retry {
  min-height: 2.25rem;
  padding: var(--kyc-space-1) var(--kyc-space-3);
  border: 1px solid var(--kyc-color-border);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-surface-raised);
  color: var(--kyc-color-text);
  font: inherit;
  font-size: var(--kyc-text-sm);
  font-weight: 600;
  cursor: pointer;
}

.retry:hover {
  border-color: var(--kyc-color-brand);
  color: var(--kyc-color-brand);
}

.retry:focus-visible {
  outline: none;
  box-shadow: var(--kyc-focus-ring);
}
</style>
