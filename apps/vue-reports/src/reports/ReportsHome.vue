<template>
  <section :class="$style['page']" aria-labelledby="reports-heading">
    <h1 id="reports-heading" :class="$style['title']">{{ copy.pageTitle }}</h1>
    <p :class="$style['lede']">{{ copy.lede }}</p>
    <p :class="$style['hint']" role="status">{{ copy.pendingHint }}</p>
  </section>
</template>

<script setup lang="ts">
import { REPORTS_HOME_MESSAGES, type ReportsHomeMessages } from './reports.messages';

defineOptions({ name: 'ReportsHome' });

/** Presentational reports landing (KYC-080). Counts/table are KYC-081. */
const copy: ReportsHomeMessages = REPORTS_HOME_MESSAGES;
</script>

<style module>
.page {
  box-sizing: border-box;
  max-width: var(--kyc-content-max);
  margin: 0 auto;
  padding: var(--kyc-space-6) var(--kyc-page-gutter);
}

.title {
  margin: 0 0 var(--kyc-space-2);
  font-size: var(--kyc-text-xl);
  font-weight: 600;
  line-height: var(--kyc-leading-tight);
  color: var(--kyc-color-text);
}

.lede {
  margin: 0 0 var(--kyc-space-4);
  color: var(--kyc-color-text-muted);
  font-size: var(--kyc-text-sm);
}

.hint {
  margin: 0;
  padding: var(--kyc-space-4);
  border: 1px dashed var(--kyc-color-border);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-surface-raised);
  color: var(--kyc-color-text-muted);
  font-size: var(--kyc-text-sm);
}

@media (max-width: 640px) {
  .page {
    padding: var(--kyc-space-5) var(--kyc-page-gutter);
  }
}
</style>
