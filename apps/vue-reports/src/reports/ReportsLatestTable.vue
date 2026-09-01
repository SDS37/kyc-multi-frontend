<template>
  <div :class="$style['tableWrap']">
    <table :class="$style['table']" :aria-labelledby="labelledBy">
      <thead>
        <tr>
          <th scope="col">{{ copy.columnTitle }}</th>
          <th scope="col">{{ copy.columnCustomer }}</th>
          <th scope="col">{{ copy.columnStatus }}</th>
          <th scope="col">{{ copy.columnUpdated }}</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in items"
          :key="row.id"
          :class="$style['row']"
        >
          <td>{{ row.title }}</td>
          <td :class="$style['email']">{{ row.customerEmail }}</td>
          <td>
            <span :class="$style['status']" :data-status="row.status">
              {{ row.statusLabel }}
            </span>
          </td>
          <td>{{ row.updatedAtLabel }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
import { REPORTS_HOME_MESSAGES, type ReportsHomeMessages } from './reports.messages';
import type { ReportCaseRow } from './reports.models';

defineOptions({ name: 'ReportsLatestTable' });

defineProps<{
  readonly items: readonly ReportCaseRow[];
  readonly labelledBy: string;
}>();

const copy: ReportsHomeMessages = REPORTS_HOME_MESSAGES;
</script>

<style module>
.tableWrap {
  overflow-x: auto;
  border: 1px solid var(--kyc-color-border);
  border-radius: var(--kyc-radius-md);
  background: var(--kyc-color-surface-raised);
  box-shadow: var(--kyc-shadow-sm);
}

.table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--kyc-text-sm);
}

.table th,
.table td {
  padding: var(--kyc-space-3) var(--kyc-space-4);
  text-align: left;
  border-bottom: 1px solid var(--kyc-color-border);
  white-space: nowrap;
}

.table th {
  font-weight: 600;
  color: var(--kyc-color-text-muted);
  background: var(--kyc-color-surface);
}

.table tbody tr:last-child td {
  border-bottom: none;
}

.row:hover {
  background: var(--kyc-color-surface);
}

.email {
  font-family: var(--kyc-font-mono);
}

.status {
  display: inline-block;
  padding: var(--kyc-space-1) var(--kyc-space-2);
  border-radius: var(--kyc-radius-sm);
  font-size: var(--kyc-text-xs);
  font-weight: 600;
  letter-spacing: 0.02em;
  background: var(--kyc-color-surface);
  color: var(--kyc-color-text);
  border: 1px solid var(--kyc-color-border);
}

.status[data-status='SUBMITTED'],
.status[data-status='IN_REVIEW'] {
  background: var(--kyc-color-warning-bg);
  color: var(--kyc-color-warning);
  border-color: transparent;
}

.status[data-status='APPROVED'] {
  background: var(--kyc-color-success-bg);
  color: var(--kyc-color-success);
  border-color: transparent;
}

.status[data-status='REJECTED'] {
  background: var(--kyc-color-danger-bg);
  color: var(--kyc-color-danger);
  border-color: transparent;
}

@media (max-width: 640px) {
  .tableWrap {
    border-radius: var(--kyc-radius-sm);
  }
}
</style>
