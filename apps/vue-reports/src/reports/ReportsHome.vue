<template>
  <section :class="$style['page']" aria-labelledby="reports-heading">
    <h1 id="reports-heading" :class="$style['title']">{{ copy.pageTitle }}</h1>
    <p :class="$style['lede']">{{ copy.lede }}</p>

    <ReportsLoadError
      v-if="loadError"
      :message="loadError"
      @retry="reload"
    />

    <div
      v-else-if="loading"
      :class="$style['loading']"
      role="status"
      aria-live="polite"
    >
      <span :class="$style['spinner']" :aria-label="copy.loadingAria" />
      <span>{{ copy.loading }}</span>
    </div>

    <template v-else-if="overview">
      <h2 id="counts-heading" :class="$style['sectionTitle']">
        {{ copy.countsHeading }}
      </h2>
      <ReportsStatusCounts
        :counts="overview.counts"
        labelled-by="counts-heading"
      />

      <h2 id="latest-heading" :class="$style['sectionTitle']">
        {{ copy.latestHeading }}
      </h2>
      <p :class="$style['hint']">{{ copy.latestHint }}</p>

      <p
        v-if="overview.latest.length === 0"
        :class="$style['empty']"
        role="status"
      >
        {{ copy.emptyLatest }}
      </p>
      <ReportsLatestTable
        v-else
        :items="overview.latest"
        labelled-by="latest-heading"
      />
    </template>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref, type Ref } from 'vue';
import ReportsLatestTable from './ReportsLatestTable.vue';
import ReportsLoadError from './ReportsLoadError.vue';
import ReportsStatusCounts from './ReportsStatusCounts.vue';
import { loadReportsOverview } from './reports-api';
import { toReportsLoadError } from './reports.mappers';
import { REPORTS_HOME_MESSAGES, type ReportsHomeMessages } from './reports.messages';
import type { ReportsOverview } from './reports.models';

defineOptions({ name: 'ReportsHome' });

/**
 * Smart reports screen (KYC-081).
 * Loads aliased `cases` counts + latest 10; leaves are presentational.
 */
const copy: ReportsHomeMessages = REPORTS_HOME_MESSAGES;
const overview: Ref<ReportsOverview | null> = ref(null);
const loading: Ref<boolean> = ref(true);
const loadError: Ref<string | null> = ref(null);
let loadSeq: number = 0;

onMounted((): void => {
  void reload();
});

async function reload(): Promise<void> {
  const seq: number = loadSeq + 1;
  loadSeq = seq;
  loading.value = true;
  loadError.value = null;
  try {
    const data: ReportsOverview = await loadReportsOverview();
    if (loadSeq !== seq) {
      return;
    }
    overview.value = data;
    loading.value = false;
  } catch (err: unknown) {
    if (loadSeq !== seq) {
      return;
    }
    overview.value = null;
    loading.value = false;
    loadError.value = toReportsLoadError(err).message;
  }
}
</script>

<style module>
.page {
  box-sizing: border-box;
  max-width: 64rem;
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
  margin: 0 0 var(--kyc-space-5);
  color: var(--kyc-color-text-muted);
  font-size: var(--kyc-text-sm);
}

.sectionTitle {
  margin: 0 0 var(--kyc-space-3);
  font-size: var(--kyc-text-md);
  font-weight: 600;
  color: var(--kyc-color-text);
}

.hint {
  margin: 0 0 var(--kyc-space-3);
  color: var(--kyc-color-text-muted);
  font-size: var(--kyc-text-sm);
}

.loading,
.empty {
  display: flex;
  align-items: center;
  gap: var(--kyc-space-3);
  margin: var(--kyc-space-4) 0;
  color: var(--kyc-color-text-muted);
  font-size: var(--kyc-text-sm);
}

.spinner {
  width: 1.5rem;
  height: 1.5rem;
  border: 2px solid var(--kyc-color-border);
  border-top-color: var(--kyc-color-brand);
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 640px) {
  .page {
    padding: var(--kyc-space-5) var(--kyc-page-gutter);
  }
}
</style>
