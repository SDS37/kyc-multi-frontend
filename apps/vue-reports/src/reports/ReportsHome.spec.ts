import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ReportsHome from './ReportsHome.vue';
import * as reportsApi from './reports-api';
import { REPORTS_HOME_MESSAGES } from './reports.messages';
import { ReportsLoadError, type ReportsOverview } from './reports.models';

const emptyOverview: ReportsOverview = {
  counts: [
    { status: 'DRAFT', label: 'Draft', count: 0 },
    { status: 'SUBMITTED', label: 'Submitted', count: 0 },
    { status: 'IN_REVIEW', label: 'In review', count: 0 },
    { status: 'APPROVED', label: 'Approved', count: 0 },
    { status: 'REJECTED', label: 'Rejected', count: 0 },
  ],
  latest: [],
  latestTotalCount: 0,
};

describe('ReportsHome', () => {
  afterEach((): void => {
    vi.restoreAllMocks();
  });

  it('renders counts and empty latest copy', async (): Promise<void> => {
    vi.spyOn(reportsApi, 'loadReportsOverview').mockResolvedValue(emptyOverview);
    const wrapper = mount(ReportsHome);
    await flushPromises();
    expect(wrapper.get('#reports-heading').text()).toBe(REPORTS_HOME_MESSAGES.pageTitle);
    expect(wrapper.get('#counts-heading').text()).toBe(REPORTS_HOME_MESSAGES.countsHeading);
    expect(wrapper.text()).toContain('Draft');
    expect(wrapper.text()).toContain(REPORTS_HOME_MESSAGES.emptyLatest);
    expect(wrapper.find('table').exists()).toBe(false);
  });

  it('renders a read-only latest table without links', async (): Promise<void> => {
    vi.spyOn(reportsApi, 'loadReportsOverview').mockResolvedValue({
      ...emptyOverview,
      counts: emptyOverview.counts.map((item) =>
        item.status === 'SUBMITTED' ? { ...item, count: 1 } : item,
      ),
      latest: [
        {
          id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          title: 'Passport check',
          status: 'SUBMITTED',
          statusLabel: 'Submitted',
          customerEmail: 'c@acme.example',
          updatedAt: '2026-09-01T12:00:00.000Z',
          updatedAtLabel: '1 Sep 2026, 12:00',
        },
      ],
      latestTotalCount: 1,
    });
    const wrapper = mount(ReportsHome);
    await flushPromises();
    expect(wrapper.get('table').text()).toContain('Passport check');
    expect(wrapper.get('table').text()).toContain('c@acme.example');
    expect(wrapper.find('table a').exists()).toBe(false);
  });

  it('shows Try again on load failure', async (): Promise<void> => {
    vi.spyOn(reportsApi, 'loadReportsOverview').mockRejectedValue(
      new ReportsLoadError(REPORTS_HOME_MESSAGES.listLoadFailed),
    );
    const wrapper = mount(ReportsHome);
    await flushPromises();
    expect(wrapper.get('[role="alert"]').text()).toContain(REPORTS_HOME_MESSAGES.listLoadFailed);
    expect(wrapper.get('button').text()).toBe('Try again');
  });
});
