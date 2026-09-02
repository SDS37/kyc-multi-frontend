<template>
  <section :class="$style['counts']" :aria-labelledby="labelledBy">
    <ul :class="$style['list']" role="list">
      <li
        v-for="item in counts"
        :key="item.status"
        :class="$style['card']"
      >
        <span :class="$style['label']">{{ item.label }}</span>
        <span :class="$style['value']">{{ item.count }}</span>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import type { StatusCount } from './reports.models';

defineOptions({ name: 'ReportsStatusCounts' });

defineProps<{
  readonly counts: readonly StatusCount[];
  readonly labelledBy: string;
}>();
</script>

<style module>
.counts {
  margin: 0 0 var(--kyc-space-5);
}

.list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: var(--kyc-space-3);
}

.card {
  display: flex;
  flex-direction: column;
  gap: var(--kyc-space-1);
  padding: var(--kyc-space-4);
  border: 1px solid var(--kyc-color-border);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-surface-raised);
  box-shadow: var(--kyc-shadow-sm);
}

.label {
  font-size: var(--kyc-text-sm);
  font-weight: 600;
  color: var(--kyc-color-text-muted);
}

.value {
  font-size: var(--kyc-text-xl);
  font-weight: 600;
  line-height: var(--kyc-leading-tight);
  color: var(--kyc-color-text);
  font-variant-numeric: tabular-nums;
}

@media (max-width: 640px) {
  .list {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
